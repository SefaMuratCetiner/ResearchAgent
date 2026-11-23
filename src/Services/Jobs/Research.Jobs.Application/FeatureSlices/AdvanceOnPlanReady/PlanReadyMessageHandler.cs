using Research.Jobs.Application.Common.Services;
using Research.Shared.Bus.Messaging;
using Research.Shared.Contracts.Planning;

namespace Research.Jobs.Application.AdvanceOnPlanReady;

/// <summary>The plan arrived: the job starts gathering with its sub-questions.</summary>
public sealed class PlanReadyMessageHandler(StepReplyProcessor replies, JobProgressService progress) : IMessageHandler<PlanReady>
{
    public Task HandleAsync(PlanReady message, MessageContext context, CancellationToken cancellationToken) =>
        replies.ProcessAsync(message, context, job => progress.ApplyPlanAsync(job, message, cancellationToken), cancellationToken);
}
