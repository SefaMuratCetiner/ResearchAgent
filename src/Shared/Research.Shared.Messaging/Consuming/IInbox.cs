namespace Research.Shared.Messaging;

/// <summary>
/// Idempotent-consumer inbox. Records every processed (message id, consumer) pair inside the active
/// <see cref="Transactions.IUnitOfWork"/> transaction so redeliveries never take effect twice.
/// </summary>
public interface IInbox
{
    Task<bool> HasProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken);

    /// <returns>True when the message was recorded for the first time; false when it was already processed.</returns>
    Task<bool> TryRecordAsync(Guid messageId, string consumer, CancellationToken cancellationToken);
}
