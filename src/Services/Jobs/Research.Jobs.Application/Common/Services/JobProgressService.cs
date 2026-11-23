using Microsoft.Extensions.Options;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Domain;
using Research.Shared.Contracts.Planning;
using Research.Shared.Contracts.Search;
using Research.Shared.Contracts.Steps;
using Research.Shared.Contracts.Synthesis;
using Research.Shared.Core.Security;

namespace Research.Jobs.Application.Common.Services;

/// <summary>
/// Advances a locked <see cref="ResearchJob"/> on each worker reply. Every method only changes the job (and writes
/// what the reply carries); <see cref="StepReplyProcessor"/> saves the job afterwards, which publishes the resulting
/// domain events. Another service's message is input like any other (OWASP ASI07, LLM05): text is bounded and cleaned.
/// </summary>
public sealed class JobProgressService(
    FetchStepService fetchSteps,
    GatheringPhaseCompleter gatheringCompleter,
    IJobStepRepository steps,
    IResearchReportRepository reports,
    IOptionsSnapshot<JobsOptions> options,
    TimeProvider timeProvider)
{
    /// <summary>The plan arrived: record its tokens and start gathering with its sub-questions.</summary>
    public Task ApplyPlanAsync(ResearchJob job, PlanReady plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(plan);
        job.RecordTokens(plan.InputTokens + plan.OutputTokens);
        if (job.Status == JobStatus.Planning)
        {
            var settings = options.Value;
            var subQuestions = Normalize(plan.SubQuestions, settings.MaxSubQuestions, settings.MaxSubQuestionLength);
            job.StartGathering(subQuestions, timeProvider.GetUtcNow());
        }

        return Task.CompletedTask;
    }

    /// <summary>A search answered: record its tokens, start a fetch step per new URL, then check whether gathering ended.</summary>
    public async Task ApplyResultsAsync(ResearchJob job, ResultsFound results, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(results);
        job.RecordTokens(results.InputTokens + results.OutputTokens);
        if (job.Status != JobStatus.Gathering)
        {
            return;
        }

        await fetchSteps.StartFetchesAsync(job, results.Hits, cancellationToken).ConfigureAwait(false);
        await gatheringCompleter.TryCompleteAsync(job, forcedReason: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A page was indexed: the job has one more source, so gathering may now end.</summary>
    public Task ApplyDocumentIndexedAsync(ResearchJob job, CancellationToken cancellationToken) =>
        gatheringCompleter.TryCompleteAsync(job, forcedReason: null, cancellationToken);

    /// <summary>
    /// The report arrived: store it and complete the job, with warnings when sources failed or the report was cut.
    /// </summary>
    public async Task ApplyReportAsync(ResearchJob job, ReportReady report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(report);
        if (job.Status != JobStatus.Synthesizing)
        {
            return;
        }

        job.RecordTokens(report.InputTokens + report.OutputTokens);

        var settings = options.Value;
        var content = report.Content ?? string.Empty;
        var cut = content.Length > settings.MaxReportChars;
        var stored = new ResearchReport
        {
            Id = job.Id,
            Content = cut ? content[..settings.MaxReportChars] : content,
            Citations = (report.Citations ?? [])
                .Where(static c => c is not null && c.Number > 0 && UntrustedText.IsWebUrl(c.Url))
                .Take(settings.MaxCitations)
                .Select(static c => new ReportSource(c.Number, c.Url, UntrustedText.SingleLine(c.Title)))
                .ToList(),
            Model = UntrustedText.SingleLine(report.Model, 100),
            Truncated = report.Truncated || cut,
        };
        await reports.InsertAsync(stored, cancellationToken).ConfigureAwait(false);

        var counts = await steps.CountAsync(job.Id, cancellationToken).ConfigureAwait(false);
        job.Complete(counts.GatheringFailed, stored.Truncated, timeProvider.GetUtcNow());
    }

    /// <summary>
    /// A worker gave up on a step. A failed plan or synthesis fails the job with the worker's code; a failed search or
    /// fetch only counts toward <c>jobs.steps_failed</c>, and gathering may now end (§10.1).
    /// </summary>
    public Task ApplyStepFailureAsync(ResearchJob job, StepFailed failure, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(failure);
        if (JobStepKeys.KindOf(failure.StepKey) is StepKind.Plan or StepKind.Synthesize)
        {
            job.Fail(failure.ErrorCode, timeProvider.GetUtcNow());
            return Task.CompletedTask;
        }

        return gatheringCompleter.TryCompleteAsync(job, forcedReason: null, cancellationToken);
    }

    /// <summary>
    /// One clean line each (cut at <paramref name="maxLength"/>), non-empty, case-insensitively distinct, at most
    /// <paramref name="max"/>.
    /// </summary>
    internal static List<string> Normalize(IReadOnlyList<string>? subQuestions, int max, int maxLength = 200) =>
        (subQuestions ?? [])
            .Select(q => UntrustedText.SingleLine(q, maxLength))
            .Where(static q => q.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();
}
