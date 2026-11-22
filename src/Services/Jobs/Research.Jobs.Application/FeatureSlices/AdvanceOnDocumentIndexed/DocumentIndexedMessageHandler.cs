using Research.Contracts.Knowledge;
using Research.Jobs.Application.Common.Services;
using Research.Jobs.Domain;
using Research.Shared.Messaging;

namespace Research.Jobs.Application.AdvanceOnDocumentIndexed;

/// <summary>A page was indexed: its fetch step succeeded and the job has one more source. Gathering may now end.</summary>
public sealed class DocumentIndexedMessageHandler(
    StepReplyProcessor replyProcessor,
    GatheringPhaseCompleter gatheringCompleter) : IMessageHandler<DocumentIndexed>
{
    public Task HandleAsync(DocumentIndexed message, MessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return replyProcessor.ProcessAsync(
            message.JobId,
            message.StepKey,
            StepStatus.Succeeded,
            stepErrorCode: null,
            context,
            job => gatheringCompleter.TryCompleteAsync(job, forcedReason: null, cancellationToken),
            cancellationToken);
    }
}
