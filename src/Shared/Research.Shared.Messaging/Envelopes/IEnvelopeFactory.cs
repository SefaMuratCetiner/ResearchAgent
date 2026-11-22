using Research.Shared.Contracts;

namespace Research.Shared.Messaging;

/// <summary>
/// Turns a contract into an <see cref="OutgoingEnvelope"/> with a message id, JSON payload, headers and trace context.
/// </summary>
public interface IEnvelopeFactory
{
    OutgoingEnvelope Create<TMessage>(TMessage message)
        where TMessage : IIntegrationMessage;

    /// <summary>
    /// Creates a new envelope for a payload serialized earlier (used to re-emit a stuck step).
    /// </summary>
    OutgoingEnvelope Recreate(string messageType, string payload, Guid jobId, string stepKey);
}
