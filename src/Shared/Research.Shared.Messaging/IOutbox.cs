namespace Research.Shared.Messaging;

/// <summary>
/// Transactional outbox. Writing an envelope here inside an active <see cref="Transactions.IUnitOfWork"/> transaction
/// commits it atomically with the database change; the outbox dispatcher then publishes it to the broker.
/// </summary>
public interface IOutbox
{
    Task EnqueueAsync(OutgoingEnvelope envelope, CancellationToken cancellationToken);
}
