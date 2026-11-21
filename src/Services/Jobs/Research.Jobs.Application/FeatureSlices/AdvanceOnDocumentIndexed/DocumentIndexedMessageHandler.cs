using Research.Contracts.Knowledge;
using Research.Jobs.Domain;
using Research.Jobs.Application.Lifecycle;
using Research.Shared.Core.Messaging;

namespace Research.Jobs.Application.AdvanceOnDocumentIndexed;

/// <summary>A page was indexed: its fetch step succeeded and the job has one more source. Gathering may now end.</summary>
public sealed class DocumentIndexedMessageHandler(JobReplyGate gate, JobLifecycle lifecycle) : IMessageHandler<DocumentIndexed>
{
    public Task HandleAsync(DocumentIndexed message, MessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return gate.ProcessAsync(
            message.JobId,
            message.StepKey,
            StepStatus.Succeeded,
            stepErrorCode: null,
            context,
            job => lifecycle.EvaluateGatheringAsync(job, forcedReason: null, cancellationToken),
            cancellationToken);
    }
}
