using Research.Shared.Kernel.Events;

namespace Research.Shared.Bus.Transactions;

/// <summary>
/// Scoped unit of work for a service's database. Coordinates one active transaction at a time across repositories,
/// inbox, outbox and domain event handlers without leaking transaction handles into domain or application ports.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>
    /// Opens a database transaction for the current operation. Disposing the returned handle without calling
    /// <see cref="CommitAsync"/> rolls back and resets the unit of work so a subsequent operation in the same scope
    /// can begin cleanly.
    /// </summary>
    Task<IAsyncDisposable> BeginAsync(CancellationToken cancellationToken);

    /// <summary>Registers <paramref name="entity"/> so its uncommitted domain events are dispatched before commit.</summary>
    void Track(IHasDomainEvents entity);

    /// <summary>Dispatches domain events of tracked entities inside the active transaction and commits.</summary>
    Task CommitAsync(CancellationToken cancellationToken);
}
