using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Research.SharedKernel.Messaging;

namespace Research.Providers.Postgres.Messaging;

/// <summary>
/// The dispatcher side of the outbox. Each run claims a batch of due rows with <c>FOR UPDATE SKIP LOCKED</c> (replicas
/// never publish the same row at the same time), publishes it, and marks rows published only after the broker
/// confirmed them. A crash after the confirm but before the commit republishes the row: consumers are idempotent. A row
/// whose publish failed is due again after the retry delay (<c>next_attempt_at</c>) and is parked (<c>parked_at</c>)
/// once it used up its attempts; a parked row stays in the table until someone requeues it.
/// </summary>
public sealed partial class PostgresOutboxStore(NpgsqlDataSource dataSource, ILogger<PostgresOutboxStore> logger)
    : IOutboxStore, IAsyncDisposable
{
    private const string ClaimSql = """
        SELECT id, message_type, payload::text AS payload, headers::text AS headers, occurred_at, attempts
        FROM outbox_messages
        WHERE published_at IS NULL
          AND parked_at IS NULL
          AND (next_attempt_at IS NULL OR next_attempt_at <= now())
        ORDER BY created_at, id
        LIMIT @BatchSize
        FOR UPDATE SKIP LOCKED
        """;

    private const string MarkPublishedSql = "UPDATE outbox_messages SET published_at = now() WHERE id = ANY(@Ids)";

    private const string MarkFailedSql = """
        UPDATE outbox_messages
        SET attempts = @Attempts,
            last_error = @Error,
            next_attempt_at = now() + @DelaySeconds * interval '1 second',
            parked_at = CASE WHEN @Park THEN now() END
        WHERE id = @Id
        """;

    private NpgsqlConnection? _listenConnection;

    public async Task<int> PublishPendingAsync(
        Func<OutgoingEnvelope, CancellationToken, Task> publish,
        int batchSize,
        OutboxRetryPolicy retry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(publish);
        ArgumentNullException.ThrowIfNull(retry);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var rows = (await connection.QueryAsync<OutboxRow>(new CommandDefinition(
            ClaimSql,
            new { BatchSize = batchSize },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();

        if (rows.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }

        var published = new List<Guid>(rows.Count);
        foreach (var row in rows)
        {
            try
            {
                await publish(row.ToEnvelope(), cancellationToken).ConfigureAwait(false);
                published.Add(row.Id);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var attempts = row.Attempts + 1;
                var park = retry.ShouldPark(attempts);
                var delay = retry.DelayAfter(attempts);
                if (park)
                {
                    LogParked(logger, exception, row.Id, row.MessageType, attempts);
                }
                else
                {
                    LogPublishFailed(logger, exception, row.Id, row.MessageType, attempts, delay.TotalSeconds);
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    MarkFailedSql,
                    new
                    {
                        row.Id,
                        Attempts = attempts,
                        Error = exception.GetType().Name + ": " + exception.Message,
                        DelaySeconds = delay.TotalSeconds,
                        Park = park,
                    },
                    transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
                break;
            }
        }

        if (published.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                MarkPublishedSql,
                new { Ids = published.ToArray() },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return published.Count;
    }

    public async Task WaitForSignalAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await GetListenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await connection.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (NpgsqlException exception)
        {
            // The listen connection broke: drop it, fall back to polling, and re-listen on the next wait.
            LogListenFailed(logger, exception);
            await ResetListenConnectionAsync().ConfigureAwait(false);
            await Task.Delay(timeout, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync() => await ResetListenConnectionAsync().ConfigureAwait(false);

    private async Task<NpgsqlConnection> GetListenConnectionAsync(CancellationToken cancellationToken)
    {
        if (_listenConnection is { State: System.Data.ConnectionState.Open })
        {
            return _listenConnection;
        }

        await ResetListenConnectionAsync().ConfigureAwait(false);
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (var listen = new NpgsqlCommand("LISTEN " + PostgresOutbox.Channel, connection))
        {
            await listen.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        _listenConnection = connection;
        return connection;
    }

    private async Task ResetListenConnectionAsync()
    {
        if (_listenConnection is not null)
        {
            await _listenConnection.DisposeAsync().ConfigureAwait(false);
            _listenConnection = null;
        }
    }

    [LoggerMessage(EventId = 8200, Level = LogLevel.Warning,
        Message = "Publishing outbox message {MessageId} ({MessageType}) failed (attempt {Attempts}); retrying in {DelaySeconds} s")]
    private static partial void LogPublishFailed(
        ILogger logger, Exception exception, Guid messageId, string messageType, int attempts, double delaySeconds);

    [LoggerMessage(EventId = 8201, Level = LogLevel.Warning, Message = "The outbox LISTEN connection failed; polling until it is re-established")]
    private static partial void LogListenFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 8202, Level = LogLevel.Error,
        Message = "Outbox message {MessageId} ({MessageType}) failed {Attempts} times; parked, it will not be retried")]
    private static partial void LogParked(ILogger logger, Exception exception, Guid messageId, string messageType, int attempts);

    private sealed record OutboxRow(Guid Id, string MessageType, string Payload, string Headers, DateTimeOffset OccurredAt, int Attempts)
    {
        public OutgoingEnvelope ToEnvelope() => new(
            Id,
            MessageType,
            Payload,
            JsonSerializer.Deserialize<Dictionary<string, string>>(Headers) ?? [],
            OccurredAt);
    }
}
