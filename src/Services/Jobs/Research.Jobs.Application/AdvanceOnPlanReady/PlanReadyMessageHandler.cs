using Microsoft.Extensions.Options;
using Research.Contracts.Planning;
using Research.Jobs.Domain;
using Research.Jobs.Lifecycle;
using Research.Shared.Core.Messaging;
using Research.Shared.Core.Security;

namespace Research.Jobs.AdvanceOnPlanReady;

/// <summary>
/// The plan arrived: record its tokens and sub-questions on <see cref="ResearchJob"/>. On commit, domain event
/// handlers emit one <c>SearchRequested</c> step per sub-question (or enqueue <c>JobFailed</c> for an empty plan).
/// </summary>
public sealed class PlanReadyMessageHandler(
    JobReplyGate gate,
    JobLifecycle lifecycle,
    TimeProvider timeProvider,
    IOptionsSnapshot<JobsOptions> options) : IMessageHandler<PlanReady>
{
    public Task HandleAsync(PlanReady message, MessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return gate.ProcessAsync(
            message.JobId,
            message.StepKey,
            StepStatus.Succeeded,
            stepErrorCode: null,
            context,
            async (job, transaction) =>
            {
                if (!await lifecycle.AddTokensAsync(job, message.InputTokens + message.OutputTokens, transaction, cancellationToken)
                        .ConfigureAwait(false)
                    || job.Status != JobStatus.Planning)
                {
                    return;
                }

                var settings = options.Value;
                var subQuestions = Normalize(message.SubQuestions, settings.MaxSubQuestions, settings.MaxSubQuestionLength);
                job.StartGathering(subQuestions, timeProvider.GetUtcNow());
                await lifecycle.SaveAsync(job, transaction, cancellationToken).ConfigureAwait(false);
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
