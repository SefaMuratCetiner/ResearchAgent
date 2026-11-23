using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Application.Observability;
using Research.Jobs.Domain;

namespace Research.Jobs.Application.Common.Services;

/// <summary>
/// Checks whether a job's <see cref="JobStatus.Gathering"/> phase has reached a stopping condition and, if so,
/// transitions the job. The caller saves it.
/// </summary>
/// <remarks>
/// Gathering ends when any of the following holds: a forced reason is supplied (such as the synthesis cutoff),
/// the job's token budget is exhausted, or all search and fetch steps have finished. The aggregate decides whether
/// enough sources were indexed to enter <see cref="JobStatus.Synthesizing"/> or to fail the job.
/// </remarks>
public sealed partial class GatheringPhaseCompleter(
    IJobStepRepository steps,
    JobsMetrics metrics,
    IOptionsSnapshot<JobsOptions> options,
    TimeProvider timeProvider,
    ILogger<GatheringPhaseCompleter> logger)
{
    /// <summary>
    /// Completes gathering for <paramref name="job"/> if a stopping condition is met.
    /// </summary>
    /// <param name="job">The locked job aggregate to inspect and update.</param>
    /// <param name="forcedReason">An explicit cutoff or override reason, or <see langword="null"/> to check budget and open step counts.</param>
    /// <param name="cancellationToken">Token to cancel the database operations.</param>
    /// <returns><see langword="true"/> if gathering ended; otherwise <see langword="false"/>.</returns>
    public async Task<bool> TryCompleteAsync(
        ResearchJob job,
        GatheringEndReason? forcedReason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.Status != JobStatus.Gathering)
        {
            return false;
        }

        var counts = await steps.CountAsync(job.Id, cancellationToken).ConfigureAwait(false);
        var reason = forcedReason;
        if (reason is null && job.BudgetExhausted)
        {
            reason = GatheringEndReason.BudgetExhausted;
        }

        if (reason is null && counts.GatheringRunning == 0)
        {
            reason = GatheringEndReason.AllStepsDone;
        }

        if (reason is null)
        {
            return false;
        }

        metrics.GatheringEnded(counts.FetchSucceeded);
        LogGatheringEnded(logger, job.Id, reason.Value, counts.FetchSucceeded, counts.GatheringFailed);

        return job.EndGathering(reason.Value, counts.FetchSucceeded, options.Value.MinSources, timeProvider.GetUtcNow());
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Job {JobId} gathering ended ({Reason}) with {Sources} sources and {FailedSteps} failed steps")]
    private static partial void LogGatheringEnded(ILogger logger, Guid jobId, GatheringEndReason reason, int sources, int failedSteps);
}
