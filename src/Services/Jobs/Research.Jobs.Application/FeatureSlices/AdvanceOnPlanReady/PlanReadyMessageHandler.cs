using Microsoft.Extensions.Options;
using Research.Shared.Contracts.Planning;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Application.Common.Services;
using Research.Jobs.Domain;
using Research.Shared.Bus.Messaging;
using Research.Shared.Core.Security;

namespace Research.Jobs.Application.AdvanceOnPlanReady;

/// <summary>
/// The plan arrived: record its tokens and sub-questions on <see cref="ResearchJob"/>. On commit, domain event
/// handlers emit one <c>SearchRequested</c> step per sub-question (or enqueue <c>JobFailed</c> for an empty plan).
/// </summary>
public sealed class PlanReadyMessageHandler(
    StepReplyProcessor replyProcessor,
    IResearchJobRepository jobs,
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
            async job =>
            {
                if (job.RecordTokens(message.InputTokens + message.OutputTokens)
                    && !await jobs.UpdateAsync(job, cancellationToken).ConfigureAwait(false))
                {
                    return;
                }

                if (job.Status != JobStatus.Planning)
                {
                    return;
                }

                var settings = options.Value;
                var subQuestions = Normalize(message.SubQuestions, settings.MaxSubQuestions, settings.MaxSubQuestionLength);
                job.StartGathering(subQuestions, timeProvider.GetUtcNow());
                await jobs.UpdateAsync(job, cancellationToken).ConfigureAwait(false);
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
