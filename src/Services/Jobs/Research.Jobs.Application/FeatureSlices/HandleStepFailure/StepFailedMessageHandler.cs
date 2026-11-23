using Research.Jobs.Application.Common.Services;
using Research.Shared.Bus.Messaging;
using Research.Shared.Contracts.Steps;

namespace Research.Jobs.Application.HandleStepFailure;

/// <summary>A worker gave up on a step: the job fails, or gathering may end.</summary>
public sealed class StepFailedMessageHandler(StepReplyProcessor replies, JobProgressService progress) : IMessageHandler<StepFailed>
{
    public Task HandleAsync(StepFailed message, MessageContext context, CancellationToken cancellationToken) =>
        replies.ProcessAsync(message, context, job => progress.ApplyStepFailureAsync(job, message, cancellationToken), cancellationToken);
}
