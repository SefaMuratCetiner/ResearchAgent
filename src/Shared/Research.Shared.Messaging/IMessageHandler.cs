using Research.Contracts;
using Research.Shared.Core.Mediation;
using Research.SharedKernel.Results;

namespace Research.Shared.Messaging;

/// <summary>Non-generic view of an incoming integration message request so pipeline behaviors (such as inbox deduplication) can read <see cref="Context"/> without reflection.</summary>
public interface IIncomingMessageRequest
{
    MessageContext Context { get; }
}

/// <summary>
/// Wraps an incoming integration message and its delivery <see cref="Context"/> as an <see cref="ICommand"/> so broker
/// deliveries run through the exact same <see cref="ISender"/> and <see cref="IPipelineBehavior{TRequest, TResponse}"/>
/// pipeline as commands and queries while keeping <c>Research.Contracts</c> free of mediation dependencies.
/// </summary>
public sealed record HandleMessage<TMessage>(TMessage Message, MessageContext Context) : ICommand, IIncomingMessageRequest
    where TMessage : class, IIntegrationMessage;

/// <summary>
/// Handles one integration message type inside the unified mediation pipeline. Throwing means "retry later"; a handled
/// business failure must be turned into an outgoing message (usually <c>StepFailed</c>) and return normally.
/// </summary>
public interface IMessageHandler<TMessage> : ICommandHandler<HandleMessage<TMessage>>
    where TMessage : class, IIntegrationMessage
{
    Task HandleAsync(TMessage message, MessageContext context, CancellationToken cancellationToken);

    async Task<Unit> IRequestHandler<HandleMessage<TMessage>, Unit>.HandleAsync(
        HandleMessage<TMessage> request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await HandleAsync(request.Message, request.Context, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
