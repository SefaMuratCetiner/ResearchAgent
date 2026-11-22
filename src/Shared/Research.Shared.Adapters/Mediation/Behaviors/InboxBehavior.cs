using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Research.Shared.Core.Mediation;
using Research.Shared.Messaging;
using Research.Shared.Kernel.Results;

namespace Research.Shared.Adapters.Mediation.Behaviors;

/// <summary>
/// Fast-path inbox deduplication for incoming integration messages (<see cref="IIncomingMessageEnvelope"/>):
/// when the host registers an <see cref="IInbox"/> and the message was already recorded for this consumer,
/// short-circuits before validation or external I/O. Concurrent delivery races remain protected by atomic
/// <see cref="IInbox.TryRecordAsync"/> inside the handler's write transaction.
/// </summary>
public sealed partial class InboxBehavior<TRequest, TResponse>(
    IServiceProvider serviceProvider,
    ILogger<InboxBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IIncomingMessageEnvelope incoming
            && serviceProvider.GetService<IInbox>() is { } inbox
            && await inbox.HasProcessedAsync(incoming.Context.MessageId, incoming.Context.Consumer, cancellationToken).ConfigureAwait(false))
        {
            LogDuplicate(logger, incoming.Context.MessageId, incoming.Context.MessageType, incoming.Context.Consumer);
            return typeof(TResponse) == typeof(Unit)
                ? (TResponse)(object)Unit.Value
                : default!;
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 1520, Level = LogLevel.Debug, Message = "Skipping already-processed message {MessageId} ({MessageType}) for {Consumer}")]
    private static partial void LogDuplicate(ILogger logger, Guid messageId, string messageType, string consumer);
}
