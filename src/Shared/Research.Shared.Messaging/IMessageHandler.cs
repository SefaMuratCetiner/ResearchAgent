using Research.Shared.Contracts;
using Research.Shared.Core.Mediation;
using Research.Shared.Kernel.Results;

namespace Research.Shared.Messaging;

/// <summary>
/// Handles one integration message type inside the unified mediation pipeline. Throwing means "retry later"; a handled
/// business failure must be turned into an outgoing message (usually <c>StepFailed</c>) and return normally.
/// </summary>
public interface IMessageHandler<TMessage> : ICommandHandler<IncomingMessageEnvelope<TMessage>>
    where TMessage : class, IIntegrationMessage
{
    Task HandleAsync(TMessage message, MessageContext context, CancellationToken cancellationToken);

    async Task<Unit> IRequestHandler<IncomingMessageEnvelope<TMessage>, Unit>.HandleAsync(
        IncomingMessageEnvelope<TMessage> request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await HandleAsync(request.Message, request.Context, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
