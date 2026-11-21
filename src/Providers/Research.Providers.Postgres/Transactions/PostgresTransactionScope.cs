using Npgsql;
using Research.Shared.Core.Events;
using Research.Shared.Core.Transactions;
using Research.SharedKernel.Common;

namespace Research.Providers.Postgres.Transactions;

/// <summary>
/// An open connection with a transaction. Tracked aggregates have their domain events dispatched inside the
/// transaction right before commit. Disposing without commit rolls back.
/// </summary>
public sealed class PostgresTransactionScope : ITransactionScope
{
    private readonly IDomainEventDispatcher? _dispatcher;
    private readonly List<BaseEntity> _tracked = [];
    private bool _committed;

    internal PostgresTransactionScope(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IDomainEventDispatcher? dispatcher = null)
    {
        Connection = connection;
        Transaction = transaction;
        _dispatcher = dispatcher;
    }

    public NpgsqlConnection Connection { get; }

    public NpgsqlTransaction Transaction { get; }

    /// <summary>The Postgres scope behind a port-typed scope; adapters call this before running SQL.</summary>
    public static PostgresTransactionScope From(ITransactionScope scope) =>
        scope as PostgresTransactionScope
        ?? throw new ArgumentException("The transaction does not belong to Postgres.", nameof(scope));

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
        if (_dispatcher is not null)
        {
            while (DrainEvents() is { Count: > 0 } events)
            {
                await _dispatcher.DispatchAsync(events, this, cancellationToken).ConfigureAwait(false);
            }
        }

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
}

internal static class NpgsqlTransactionExtensions
{
    /// <summary>True once the transaction was committed or rolled back (its connection is then detached).</summary>
    public static bool IsCompleted(this NpgsqlTransaction transaction) => transaction.Connection is null;
}
