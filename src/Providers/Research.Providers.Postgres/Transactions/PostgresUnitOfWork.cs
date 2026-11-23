using Npgsql;
using Research.Shared.Core.Actors;
using Research.Shared.Bus.Transactions;

namespace Research.Providers.Postgres.Transactions;

/// <summary>
/// Scoped Postgres unit of work. Holds the active connection/transaction, provides automatic audit timestamps and
/// actor identity to Postgres repositories.
/// </summary>
public sealed class PostgresUnitOfWork : IUnitOfWork
{
    private readonly TimeProvider _timeProvider;
    private readonly ICurrentActor? _actor;
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;
    private bool _committed;

    public PostgresUnitOfWork(
        NpgsqlDataSource dataSource,
        TimeProvider? timeProvider = null,
        ICurrentActor? actor = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        DataSource = dataSource;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _actor = actor;
    }

    public NpgsqlDataSource DataSource { get; }

    public DateTimeOffset Now => _timeProvider.GetUtcNow();

    public string ActorId => _actor?.Id ?? "system";

    public NpgsqlConnection ActiveConnection =>
        _connection ?? throw new InvalidOperationException("No active transaction. Call IUnitOfWork.BeginAsync first.");

    public NpgsqlTransaction ActiveTransaction =>
        _transaction ?? throw new InvalidOperationException("No active transaction. Call IUnitOfWork.BeginAsync first.");

    /// <summary>
    /// Returns the active transaction's connection when a transaction is open, or opens a short-lived connection from
    /// <see cref="DataSource"/> otherwise. Disposing the lease closes only connections it opened.
    /// </summary>
    public async ValueTask<ConnectionLease> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null && _connection is not null)
        {
            return new ConnectionLease(_connection, _transaction, owned: false);
        }

        var connection = await DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return new ConnectionLease(connection, transaction: null, owned: true);
    }

    public async Task<IAsyncDisposable> BeginAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A transaction is already active on this unit of work.");
        }

        var connection = await DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            _connection = connection;
            _transaction = transaction;
            _committed = false;
            return new TransactionLease(this);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await ActiveTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _committed = true;
    }

    public ValueTask DisposeAsync() => CloseCurrentAsync();

    private async ValueTask CloseCurrentAsync()
    {
        var transaction = _transaction;
        var connection = _connection;
        var committed = _committed;

        _transaction = null;
        _connection = null;
        _committed = false;

        if (transaction is not null)
        {
            if (!committed && transaction.Connection is not null)
            {
                try
                {
                    await transaction.RollbackAsync().ConfigureAwait(false);
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

            await transaction.DisposeAsync().ConfigureAwait(false);
        }

        if (connection is not null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    public readonly struct ConnectionLease : IAsyncDisposable
    {
        private readonly bool _owned;

        internal ConnectionLease(NpgsqlConnection connection, NpgsqlTransaction? transaction, bool owned)
        {
            Connection = connection;
            Transaction = transaction;
            _owned = owned;
        }

        public NpgsqlConnection Connection { get; }

        public NpgsqlTransaction? Transaction { get; }

        public ValueTask DisposeAsync() => _owned ? Connection.DisposeAsync() : ValueTask.CompletedTask;
    }

    private sealed class TransactionLease(PostgresUnitOfWork owner) : IAsyncDisposable
    {
        private int _disposed;

        public ValueTask DisposeAsync() =>
            Interlocked.Exchange(ref _disposed, 1) == 0 ? owner.CloseCurrentAsync() : ValueTask.CompletedTask;
    }
}
