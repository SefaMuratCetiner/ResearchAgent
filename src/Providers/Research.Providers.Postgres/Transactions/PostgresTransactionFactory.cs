using Npgsql;
using Research.Shared.Core.Events;
using Research.Shared.Core.Transactions;

namespace Research.Providers.Postgres.Transactions;

/// <summary>Opens a pooled connection from the service's data source and begins a read-committed transaction.</summary>
public sealed class PostgresTransactionFactory(NpgsqlDataSource dataSource, IDomainEventDispatcher? dispatcher = null) : ITransactionFactory
{
    public async Task<ITransactionScope> BeginAsync(CancellationToken cancellationToken)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            return new PostgresTransactionScope(connection, transaction, dispatcher);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
