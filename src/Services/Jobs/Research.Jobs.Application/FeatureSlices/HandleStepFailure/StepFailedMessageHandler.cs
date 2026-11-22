using Research.Shared.Contracts.Steps;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Application.Common.Services;
using Research.Jobs.Domain;
using Research.Shared.Messaging;

namespace Research.Jobs.Application.HandleStepFailure;

/// <summary>
/// A worker gave up on a step. A failed plan or synthesis fails the job with the worker's code; a failed search or
/// fetch only counts toward <c>jobs.steps_failed</c>, and gathering may now end (§10.1).
/// </summary>
public sealed class StepFailedMessageHandler(
    StepReplyProcessor replyProcessor,
    IResearchJobRepository jobs,
    GatheringPhaseCompleter gatheringCompleter,
    TimeProvider timeProvider) : IMessageHandler<StepFailed>
{
    public Task HandleAsync(StepFailed message, MessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return replyProcessor.ProcessAsync(
            message.JobId,
            message.StepKey,
            StepStatus.Failed,
            message.ErrorCode,
            context,
            async job =>
            {
                switch (JobStepKeys.KindOf(message.StepKey))
                {
                    case StepKind.Plan or StepKind.Synthesize:
                        if (job.Fail(message.ErrorCode, timeProvider.GetUtcNow()))
                        {
                            await jobs.UpdateAsync(job, cancellationToken).ConfigureAwait(false);
                        }

                        break;
                    default:
                        await gatheringCompleter.TryCompleteAsync(job, forcedReason: null, cancellationToken).ConfigureAwait(false);
                        break;
                }
            },
            cancellationToken);
    }
}
