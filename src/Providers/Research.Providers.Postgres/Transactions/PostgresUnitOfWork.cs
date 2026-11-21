using Npgsql;
using Research.Shared.Core.Actors;
using Research.Shared.Core.Events;
using Research.Shared.Core.Transactions;
using Research.SharedKernel.Common;

namespace Research.Providers.Postgres.Transactions;

/// <summary>
/// Scoped Postgres unit of work. Holds the active connection/transaction, provides automatic audit timestamps and
/// actor identity to Postgres repositories, tracks modified aggregates, and dispatches uncommitted domain events
/// inside the transaction right before committing.
/// </summary>
public sealed class PostgresUnitOfWork : IUnitOfWork
{
    private readonly IDomainEventDispatcher? _dispatcher;
    private readonly TimeProvider _timeProvider;
    private readonly ICurrentActor? _actor;
    private readonly List<BaseEntity> _tracked = [];
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;
    private bool _committed;

    public PostgresUnitOfWork(
        NpgsqlDataSource dataSource,
        TimeProvider? timeProvider = null,
        ICurrentActor? actor = null,
        IDomainEventDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        DataSource = dataSource;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _actor = actor;
        _dispatcher = dispatcher;
    }

    public NpgsqlDataSource DataSource { get; }

    public DateTimeOffset Now => _timeProvider.GetUtcNow();

    public string ActorId => _actor?.Id ?? "system";

    public NpgsqlConnection ActiveConnection =>
        _connection ?? throw new InvalidOperationException("No active transaction. Call IUnitOfWork.BeginAsync first.");

    public NpgsqlTransaction ActiveTransaction =>
        _transaction ?? throw new InvalidOperationException("No active transaction. Call IUnitOfWork.BeginAsync first.");

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
            _tracked.Clear();
            return new TransactionLease(this);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public void Track(BaseEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (!_tracked.Contains(entity))
        {
            _tracked.Add(entity);
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        var transaction = ActiveTransaction;
        if (_dispatcher is not null)
        {
            while (DrainEvents() is { Count: > 0 } events)
            {
                await _dispatcher.DispatchAsync(events, cancellationToken).ConfigureAwait(false);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
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
        _tracked.Clear();

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

    private List<IDomainEvent> DrainEvents()
    {
        var events = new List<IDomainEvent>();
        foreach (var entity in _tracked)
        {
            if (entity.DomainEvents.Count > 0)
            {
                events.AddRange(entity.DomainEvents);
                entity.ClearDomainEvents();
            }
        }

        return events;
    }

    private sealed class TransactionLease(PostgresUnitOfWork owner) : IAsyncDisposable
    {
        private int _disposed;

        public ValueTask DisposeAsync() =>
            Interlocked.Exchange(ref _disposed, 1) == 0 ? owner.CloseCurrentAsync() : ValueTask.CompletedTask;
    }
}
