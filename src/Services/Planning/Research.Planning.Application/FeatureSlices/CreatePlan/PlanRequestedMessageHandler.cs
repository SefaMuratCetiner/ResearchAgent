using Research.Contracts.Planning;
using Research.Contracts.Steps;
using Research.Shared.Core.Messaging;
using Research.Shared.Core.Messaging;

namespace Research.Planning.Application.CreatePlan;

/// <summary>Answers <c>PlanRequested</c> with <c>PlanReady</c>, or <c>StepFailed</c> for a business failure.</summary>
public sealed class PlanRequestedMessageHandler(CreatePlanHandler handler, IMessageSender sender) : IMessageHandler<PlanRequested>
{
    public const string ServiceName = "planning";

    public async Task HandleAsync(PlanRequested message, MessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var result = await handler.HandleAsync(new CreatePlanCommand(message.Question, message.MaxSubQuestions), cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            await sender.SendAsync(
                new PlanReady(message.JobId, message.StepKey, result.Value.SubQuestions, result.Value.InputTokens, result.Value.OutputTokens),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await sender.SendAsync(new StepFailed(message.JobId, message.StepKey, result.Error.Code, ServiceName), cancellationToken)
            .ConfigureAwait(false);
    }
}
