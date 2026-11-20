using Research.Shared.Core.Transactions;

namespace Research.Shared.Core.Messaging;

/// <summary>
/// The idempotent-consumer record: one row per (message id, consumer). Recording it in the same transaction as the
/// handler's state change makes redelivered messages harmless.
/// </summary>
public interface IInbox
{
    /// <summary>Records the message as processed; false when it already was (the caller then skips its work).</summary>
    Task<bool> TryRecordAsync(Guid messageId, string consumer, ITransactionScope transaction, CancellationToken cancellationToken);

    /// <summary>A cheap pre-check before expensive, non-transactional work (an LLM call, a download).</summary>
    Task<bool> HasProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken);
}
