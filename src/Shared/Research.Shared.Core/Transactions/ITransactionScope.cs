namespace Research.Shared.Core.Transactions;

/// <summary>
/// One open database transaction. Repositories, the outbox and the inbox take it as a parameter, so a state change,
/// its idempotency record and its outgoing messages commit together or not at all. Disposing without
/// <see cref="CommitAsync"/> rolls back.
/// </summary>
public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
