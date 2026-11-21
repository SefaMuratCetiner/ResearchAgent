using Microsoft.Extensions.Options;
using Research.Contracts.Planning;
using Research.Contracts.Search;
using Research.Jobs.Domain;
using Research.Jobs.Lifecycle;
using Research.Shared.Core.Messaging;
using Research.Shared.Core.Security;

namespace Research.Jobs.AdvanceOnPlanReady;

/// <summary>
/// The plan arrived: record its tokens and sub-questions on <see cref="ResearchJob"/>, move the job to
/// <see cref="JobStatus.Gathering"/>, and start one search step per sub-question. An empty plan fails the job with
/// <c>jobs.plan_empty</c>.
/// </summary>
public sealed class PlanReadyMessageHandler(
    JobReplyGate gate,
    JobLifecycle lifecycle,
    StepEmitter emitter,
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
                if (subQuestions.Count == 0)
                {
                    await lifecycle.FailAsync(job, JobsErrors.PlanEmpty.Code, transaction, cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (!job.StartGathering(subQuestions, timeProvider.GetUtcNow())
                    || !await lifecycle.SaveAsync(job, transaction, cancellationToken).ConfigureAwait(false))
                {
                    return;
                }

                for (var i = 0; i < subQuestions.Count; i++)
                {
                    await emitter.EmitAsync(
                        new SearchRequested(job.Id, JobStepKeys.Search(i), subQuestions[i], settings.ResultsPerSearch),
                        transaction,
                        cancellationToken).ConfigureAwait(false);
                }
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
