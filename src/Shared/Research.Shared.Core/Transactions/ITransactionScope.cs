using Research.SharedKernel.Common;

namespace Research.Shared.Core.Transactions;

/// <summary>
/// One open database transaction. Repositories, the outbox and the inbox take it as a parameter, so a state change,
/// its idempotency record and its outgoing messages commit together or not at all. Tracked entities have their
/// domain events dispatched inside the transaction right before <see cref="CommitAsync"/> commits. Disposing without
/// <see cref="CommitAsync"/> rolls back.
/// </summary>
public interface ITransactionScope : IAsyncDisposable
{
    /// <summary>Registers <paramref name="entity"/> so its uncommitted domain events are dispatched before commit.</summary>
    void Track(BaseEntity entity);

    Task CommitAsync(CancellationToken cancellationToken);
}
