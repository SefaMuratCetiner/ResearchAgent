using Research.Shared.Contracts;

namespace Research.Shared.Bus.Messaging;

/// <summary>
/// Sends a message straight to the broker, waiting for its confirm. Only for the stateless services (Planning,
/// Search, Synthesis), which have no database and therefore no outbox: they send their reply, and only then is the
/// incoming delivery acknowledged. A crash in between redelivers the request and sends the reply again; Jobs' inbox
/// and compare-and-set transitions make the duplicate harmless. Services with a database use the outbox instead.
/// </summary>
public interface IMessageSender
{
    Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : IIntegrationMessage;
}
