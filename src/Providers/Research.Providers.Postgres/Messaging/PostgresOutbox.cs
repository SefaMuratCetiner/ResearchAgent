using System.Text.Json;
using Dapper;
using Research.Providers.Postgres.Transactions;
using Research.SharedKernel.Messaging;
using Research.SharedKernel.Transactions;

namespace Research.Providers.Postgres.Messaging;

/// <summary>
/// Inserts the message in the caller's transaction and queues a NOTIFY, which Postgres delivers only on commit, so
/// the dispatcher wakes exactly when there is something committed to publish.
/// </summary>
public sealed class PostgresOutbox : IOutbox
{
    public const string Channel = "research_outbox";

    private const string Sql = """
        INSERT INTO outbox_messages (id, message_type, payload, headers, occurred_at)
        VALUES (@Id, @MessageType, @Payload::jsonb, @Headers::jsonb, @OccurredAt);
        SELECT pg_notify('research_outbox', '');
        """;

    public async Task EnqueueAsync(OutgoingEnvelope envelope, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var scope = PostgresTransactionScope.From(transaction);

        await scope.Connection.ExecuteAsync(new CommandDefinition(
            Sql,
            new
            {
                Id = envelope.MessageId,
                envelope.MessageType,
                envelope.Payload,
                Headers = JsonSerializer.Serialize(envelope.Headers),
                envelope.OccurredAt,
            },
            scope.Transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
