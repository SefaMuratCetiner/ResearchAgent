namespace Research.Shared.Bus.Messaging;

/// <summary>
/// Sends one envelope to the broker and returns once the broker confirmed it. Only the outbox dispatcher calls this;
/// business code always goes through <see cref="IOutbox"/>, so nothing is published that was not committed.
/// </summary>
public interface IMessagePublisher
{
    Task PublishAsync(OutgoingEnvelope envelope, CancellationToken cancellationToken);
}
