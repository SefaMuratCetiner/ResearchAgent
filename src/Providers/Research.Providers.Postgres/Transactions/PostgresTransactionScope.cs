using Npgsql;
using Research.Shared.Core.Transactions;

namespace Research.Providers.Postgres.Transactions;

/// <summary>An open connection with a transaction. Disposing without commit rolls back.</summary>
public sealed class PostgresTransactionScope : ITransactionScope
{
    private bool _committed;

    internal PostgresTransactionScope(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        Connection = connection;
        Transaction = transaction;
    }

    public NpgsqlConnection Connection { get; }

    public NpgsqlTransaction Transaction { get; }

    /// <summary>The Postgres scope behind a port-typed scope; adapters call this before running SQL.</summary>
    public static PostgresTransactionScope From(ITransactionScope scope) =>
        scope as PostgresTransactionScope
        ?? throw new ArgumentException("The transaction does not belong to Postgres.", nameof(scope));

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await Transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_committed && !Transaction.IsCompleted())
        {
            try
            {
                await Transaction.RollbackAsync().ConfigureAwait(false);
            }
            catch (NpgsqlException)
            {
                // The connection is broken; closing it below discards the transaction anyway.
            }
            catch (InvalidOperationException)
            {
                // Already completed by the server (e.g. after a failed statement).
            }
        }

        await Transaction.DisposeAsync().ConfigureAwait(false);
        await Connection.DisposeAsync().ConfigureAwait(false);
    }
}

internal static class NpgsqlTransactionExtensions
{
    /// <summary>True once the transaction was committed or rolled back (its connection is then detached).</summary>
    public static bool IsCompleted(this NpgsqlTransaction transaction) => transaction.Connection is null;
}
