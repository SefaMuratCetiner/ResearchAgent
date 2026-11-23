using Microsoft.Extensions.Options;
using Research.Shared.Contracts.Planning;
using Research.Jobs.Application.Common.Services;
using Research.Jobs.Domain;
using Research.Shared.Bus.Messaging;
using Research.Shared.Core.Security;

namespace Research.Jobs.Application.AdvanceOnPlanReady;

/// <summary>
/// The plan arrived: record its tokens and sub-questions on <see cref="ResearchJob"/>. Saving the job publishes
/// <c>JobGatheringStarted</c> (one <c>SearchRequested</c> step per sub-question) or <c>JobFailed</c> for an empty plan.
/// </summary>
public sealed class PlanReadyMessageHandler(
    StepReplyProcessor replyProcessor,
    TimeProvider timeProvider,
    IOptionsSnapshot<JobsOptions> options) : IMessageHandler<PlanReady>
{
    public Task HandleAsync(PlanReady message, MessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return replyProcessor.ProcessAsync(
            message.JobId,
            message.StepKey,
            StepStatus.Succeeded,
            stepErrorCode: null,
            context,
            job =>
            {
                job.RecordTokens(message.InputTokens + message.OutputTokens);
                if (job.Status == JobStatus.Planning)
                {
                    var settings = options.Value;
                    var subQuestions = Normalize(message.SubQuestions, settings.MaxSubQuestions, settings.MaxSubQuestionLength);
                    job.StartGathering(subQuestions, timeProvider.GetUtcNow());
                }

                return Task.CompletedTask;
            },
            cancellationToken);
    }

    /// <summary>
    /// One clean line each (cut at <paramref name="maxLength"/>), non-empty, case-insensitively distinct, at most
    /// <paramref name="max"/>. Another service's message is input like any other (OWASP ASI07).
    /// </summary>
    internal static List<string> Normalize(IReadOnlyList<string>? subQuestions, int max, int maxLength = 200) =>
        (subQuestions ?? [])
            .Select(q => UntrustedText.SingleLine(q, maxLength))
            .Where(static q => q.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();
}
