using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Application.Observability;
using Research.Jobs.Domain;

namespace Research.Jobs.Application.Common.Services;

/// <summary>
/// Checks whether a job's <see cref="JobStatus.Gathering"/> phase has reached a stopping condition and, if so,
/// transitions and persists the job within the active unit of work.
/// </summary>
/// <remarks>
/// Gathering ends when any of the following holds: a forced reason is supplied (such as the synthesis cutoff),
/// the job's token budget is exhausted, or all search and fetch steps have finished. The aggregate decides whether
/// enough sources were indexed to enter <see cref="JobStatus.Synthesizing"/> or to fail the job; domain event
/// handlers enqueue the resulting follow-up messages before commit.
/// </remarks>
public sealed partial class GatheringPhaseCompleter(
    IResearchJobRepository jobs,
    IJobStepRepository steps,
    JobsMetrics metrics,
    IOptionsSnapshot<JobsOptions> options,
    TimeProvider timeProvider,
    ILogger<GatheringPhaseCompleter> logger)
{
    /// <summary>
    /// Completes gathering for <paramref name="job"/> if a stopping condition is met and saves the transition.
    /// </summary>
    /// <param name="job">The locked job aggregate to inspect and update.</param>
    /// <param name="forcedReason">An explicit cutoff or override reason, or <see langword="null"/> to check budget and open step counts.</param>
    /// <param name="cancellationToken">Token to cancel the database operations.</param>
    /// <returns><see langword="true"/> if gathering ended and the updated job was persisted; otherwise <see langword="false"/>.</returns>
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

        if (!job.EndGathering(reason.Value, counts.FetchSucceeded, options.Value.MinSources, timeProvider.GetUtcNow()))
        {
            return false;
        }

        var saved = await jobs.UpdateAsync(job, cancellationToken).ConfigureAwait(false);
        if (!saved)
        {
            LogTransitionLost(logger, job.Id, job.Status);
            return false;
        }

        return true;
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Job {JobId} transition to {To} lost a race; nothing changed")]
    private static partial void LogTransitionLost(ILogger logger, Guid jobId, JobStatus to);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Job {JobId} gathering ended ({Reason}) with {Sources} sources and {FailedSteps} failed steps")]
    private static partial void LogGatheringEnded(ILogger logger, Guid jobId, GatheringEndReason reason, int sources, int failedSteps);
}
