using Dapper;
using Research.Providers.Postgres.Transactions;
using Research.Shared.Bus.Messaging;

namespace Research.Providers.Postgres.Messaging;

/// <summary>The idempotent-consumer table. The primary key (message id, consumer) does the deduplication.</summary>
public sealed class PostgresInbox(PostgresUnitOfWork unitOfWork) : IInbox
{
    private const string RecordSql = """
        INSERT INTO inbox_messages (message_id, consumer) VALUES (@MessageId, @Consumer)
        ON CONFLICT (message_id, consumer) DO NOTHING
        """;

    private const string ExistsSql = """
        SELECT EXISTS (SELECT 1 FROM inbox_messages WHERE message_id = @MessageId AND consumer = @Consumer)
        """;

    public async Task<bool> TryRecordAsync(Guid messageId, string consumer, CancellationToken cancellationToken)
    {
        var inserted = await unitOfWork.ActiveConnection.ExecuteAsync(new CommandDefinition(
            RecordSql,
            new { MessageId = messageId, Consumer = consumer },
            unitOfWork.ActiveTransaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return inserted == 1;
    }

    public async Task<bool> HasProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken)
    {
        await using var connection = await unitOfWork.DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            ExistsSql,
            new { MessageId = messageId, Consumer = consumer },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
