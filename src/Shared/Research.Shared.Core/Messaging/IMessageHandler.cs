namespace Research.Shared.Core.Messaging;

/// <summary>
/// Handles one integration message type. Throwing means "retry later"; a handled business failure must be turned
/// into an outgoing message (usually <c>StepFailed</c>) and return normally.
/// </summary>
public interface IMessageHandler<in TMessage>
{
    Task HandleAsync(TMessage message, MessageContext context, CancellationToken cancellationToken);
}
