using Research.Shared.Core.Transactions;

namespace Research.Shared.Core.Messaging;

/// <summary>
/// The transactional outbox: stores an outgoing message in the same transaction as the state change that caused it.
/// The dispatcher publishes it after commit.
/// </summary>
public interface IOutbox
{
    Task EnqueueAsync(OutgoingEnvelope envelope, ITransactionScope transaction, CancellationToken cancellationToken);
}
