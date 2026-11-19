using Research.Contracts;
using Research.SharedKernel.Messaging;

namespace Research.Messaging.Sending;

/// <summary>The <see cref="IMessageSender"/> of the stateless services: envelope, then a confirmed publish.</summary>
public sealed class DirectMessageSender(EnvelopeFactory envelopes, IMessagePublisher publisher) : IMessageSender
{
    public Task SendAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : IIntegrationMessage =>
        publisher.PublishAsync(envelopes.Create(message), cancellationToken);
}
