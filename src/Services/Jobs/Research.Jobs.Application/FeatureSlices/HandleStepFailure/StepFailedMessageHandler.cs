using Research.Contracts.Steps;
using Research.Jobs.Domain;
using Research.Jobs.Application.Lifecycle;
using Research.Shared.Messaging;

namespace Research.Jobs.Application.HandleStepFailure;

/// <summary>
/// A worker gave up on a step. A failed plan or synthesis fails the job with the worker's code; a failed search or
/// fetch only counts toward <c>jobs.steps_failed</c>, and gathering may now end (§10.1).
/// </summary>
public sealed class StepFailedMessageHandler(JobReplyGate gate, JobLifecycle lifecycle) : IMessageHandler<StepFailed>
{
    public Task HandleAsync(StepFailed message, MessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return gate.ProcessAsync(
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
                        await lifecycle.FailAsync(job, message.ErrorCode, cancellationToken).ConfigureAwait(false);
                        break;
                    default:
                        await lifecycle.EvaluateGatheringAsync(job, forcedReason: null, cancellationToken).ConfigureAwait(false);
                        break;
                }
            },
            cancellationToken);
    }
}
