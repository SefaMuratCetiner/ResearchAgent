using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Domain;
using Research.Jobs.Observability;
using Research.SharedKernel.Results;

namespace Research.Jobs.Lifecycle;

/// <summary>
/// Persists <see cref="ResearchJob"/> aggregate transitions with compare-and-set updates. Outgoing integration
/// messages and follow-up steps are produced automatically by domain event handlers right before transaction commit.
/// </summary>
public sealed partial class JobLifecycle(
    IResearchJobRepository jobs,
    IJobStepRepository steps,
    JobsMetrics metrics,
    IOptionsSnapshot<JobsOptions> options,
    TimeProvider timeProvider,
    ILogger<JobLifecycle> logger)
{
    /// <summary>Saves pending mutations on <paramref name="job"/> if the database row still matches its persisted state.</summary>
    public async Task<bool> SaveAsync(ResearchJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        var fromStatus = job.PersistedStatus;
        var saved = await jobs.UpdateAsync(job, cancellationToken).ConfigureAwait(false);
        if (!saved)
        {
            LogTransitionLost(logger, job.Id, fromStatus, job.Status);
            return false;
        }

        return true;
    }

    /// <summary>Records billed tokens on <paramref name="job"/> and saves it.</summary>
    public Task<bool> AddTokensAsync(ResearchJob job, long tokens, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        return !job.RecordTokens(tokens)
            ? Task.FromResult(true)
            : SaveAsync(job, cancellationToken);
    }

    /// <summary>
    /// Ends gathering when it is over (§10.1): every search and fetch step finished, or <paramref name="forcedReason"/>
    /// (cutoff, budget). Delegates the decision to <see cref="ResearchJob.EndGathering"/>; domain event handlers emit
    /// <c>SynthesisRequested</c> or <c>JobFailed</c> on commit.
    /// </summary>
    public async Task<bool> EvaluateGatheringAsync(
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
        GatheringEndReason? reason = forcedReason;
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

        return job.EndGathering(reason.Value, counts.FetchSucceeded, options.Value.MinSources, timeProvider.GetUtcNow())
            && await SaveAsync(job, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> FailAsync(ResearchJob job, string errorCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        return job.Fail(errorCode, timeProvider.GetUtcNow())
            && await SaveAsync(job, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExpireAsync(ResearchJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        return job.Expire(timeProvider.GetUtcNow())
            && await SaveAsync(job, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ResearchJob>> CancelAsync(ResearchJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        var wasCancelled = job.Status == JobStatus.Cancelled;
        var decision = job.Cancel(timeProvider.GetUtcNow());
        if (!decision.IsSuccess)
        {
            return Result<ResearchJob>.Failure(decision.Error);
        }

        if (wasCancelled)
        {
            return Result<ResearchJob>.Success(job);
        }

        if (!await SaveAsync(job, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The cancel transition failed although the job row is locked.");
        }

        return Result<ResearchJob>.Success(job);
    }

    /// <summary>Moves a synthesizing job to its final state once the report is stored.</summary>
    public async Task<bool> CompleteAsync(ResearchJob job, bool reportTruncated, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        var counts = await steps.CountAsync(job.Id, cancellationToken).ConfigureAwait(false);
        return job.Complete(counts.GatheringFailed, reportTruncated, timeProvider.GetUtcNow())
            && await SaveAsync(job, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Job {JobId} transition {From} -> {To} lost a race; nothing changed")]
    private static partial void LogTransitionLost(ILogger logger, Guid jobId, JobStatus from, JobStatus to);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Job {JobId} gathering ended ({Reason}) with {Sources} sources and {FailedSteps} failed steps")]
    private static partial void LogGatheringEnded(ILogger logger, Guid jobId, GatheringEndReason reason, int sources, int failedSteps);
}
