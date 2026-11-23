using Research.Shared.Contracts;
using Research.Shared.Bus.Mediation;

namespace Research.Shared.Bus.Messaging;

/// <summary>
/// Wraps an incoming integration message and its delivery <see cref="Context"/> as an <see cref="ICommand"/> so broker
/// deliveries run through the exact same <see cref="ISender"/> and <see cref="IPipelineBehavior{TRequest, TResponse}"/>
/// pipeline as commands and queries while keeping <c>Research.Shared.Contracts</c> free of mediation dependencies.
/// </summary>
public sealed record IncomingMessageEnvelope<TMessage>(TMessage Message, MessageContext Context)
    : ICommand, IIncomingMessageEnvelope, IJobScopedRequest
    where TMessage : class, IIntegrationMessage
{
    public Guid JobId => Message.JobId;
}
