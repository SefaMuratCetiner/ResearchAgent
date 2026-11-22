using Research.Shared.Contracts;
using Research.Shared.Bus.Messaging;

namespace Research.Shared.Bus.Messaging.Sending;

/// <summary>The <see cref="IMessageSender"/> of the stateless services: envelope, then a confirmed publish.</summary>
internal sealed class DirectMessageSender(IEnvelopeFactory envelopes, IMessagePublisher publisher) : IMessageSender
{
    public Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : IIntegrationMessage =>
        publisher.PublishAsync(envelopes.Create(message), cancellationToken);
}
