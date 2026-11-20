using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Contracts.Jobs;
using Research.Contracts.Synthesis;
using Research.Jobs.Domain;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Observability;
using Research.Shared.Core.Actors;
using Research.Shared.Core.Messaging;
using Research.Shared.Core.Transactions;

namespace Research.Jobs.Lifecycle;

/// <summary>
/// The transitions every slice shares, each a compare-and-set save plus its outgoing messages in the caller's
/// transaction. Callers hold the job's row lock (<see cref="IResearchJobRepository.GetForUpdateAsync"/>), so the CAS
/// only fails when the caller's copy is stale, which these methods report as <c>null</c> (nothing changed).
/// </summary>
public sealed partial class JobLifecycle(
    IResearchJobRepository jobs,
    IJobStepRepository steps,
    StepEmitter emitter,
    IOutbox outbox,
    IEnvelopeFactory envelopes,
    JobsMetrics metrics,
    IOptionsSnapshot<JobsOptions> options,
    TimeProvider timeProvider,
    ICurrentActor actor,
    ILogger<JobLifecycle> logger)
{
    private const string JobStepKey = "job";

    /// <summary>Saves <paramref name="updated"/> if <paramref name="current"/> is still what the database holds.</summary>
    public async Task<ResearchJob?> SaveAsync(ResearchJob current, ResearchJob updated, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(updated);

        IReadOnlyList<JobStatus> allowedFrom = current.Status == updated.Status
            ? [current.Status]
            : JobStateMachine.AllowedFrom(updated.Status);
        var stamped = updated with
        {
            Version = current.Version + 1,
            UpdatedAt = timeProvider.GetUtcNow(),
            UpdatedBy = actor.Id,
        };

        var saved = await jobs.UpdateAsync(stamped, current.Version, allowedFrom, transaction, cancellationToken).ConfigureAwait(false);
        if (!saved)
        {
            LogTransitionLost(logger, current.Id, current.Status, updated.Status);
            return null;
        }

        return stamped;
    }

    /// <summary>Adds an answer's tokens to the job.</summary>
    public Task<ResearchJob?> AddTokensAsync(ResearchJob job, long tokens, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        return tokens <= 0
            ? Task.FromResult<ResearchJob?>(job)
            : SaveAsync(job, job with { TokensUsed = job.TokensUsed + tokens }, transaction, cancellationToken);
    }

    /// <summary>
    /// Ends gathering when it is over (§10.1): every search and fetch step finished, or <paramref name="forcedReason"/>
    /// (cutoff, budget). Then either fails the job for too few sources or moves it to synthesis, publishing
    /// <c>SynthesisRequested</c> exactly once. Does nothing while gathering continues.
    /// </summary>
    public async Task<ResearchJob?> EvaluateGatheringAsync(
        ResearchJob job,
        GatheringEndReason? forcedReason,
        ITransactionScope transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.Status != JobStatus.Gathering)
        {
            return job;
        }

        var counts = await steps.CountAsync(job.Id, transaction, cancellationToken).ConfigureAwait(false);
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
            return job;
        }

        metrics.GatheringEnded(counts.FetchSucceeded);
        LogGatheringEnded(logger, job.Id, reason.Value, counts.FetchSucceeded, counts.GatheringFailed);

        var failure = JobOutcomePolicy.EvaluateSources(counts.FetchSucceeded, options.Value.MinSources);
        if (failure is not null)
        {
            return await FailAsync(job with { GatheringEndReason = reason }, failure.Code, transaction, cancellationToken).ConfigureAwait(false);
        }

        var synthesizing = await SaveAsync(
            job,
            job with { Status = JobStatus.Synthesizing, GatheringEndReason = reason },
            transaction,
            cancellationToken).ConfigureAwait(false);
        if (synthesizing is null)
        {
            return null;
        }

        await emitter.EmitAsync(
            new SynthesisRequested(job.Id, JobStepKeys.Synthesize, job.Question, job.Plan, options.Value.SynthesisTopK),
            transaction,
            cancellationToken).ConfigureAwait(false);
        return synthesizing;
    }

    public async Task<ResearchJob?> FailAsync(ResearchJob job, string errorCode, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        var failed = await FinishAsync(job, JobStatus.Failed, errorCode, [], transaction, cancellationToken).ConfigureAwait(false);
        if (failed is not null)
        {
            await outbox.EnqueueAsync(envelopes.Create(new JobFailed(job.Id, JobStepKey, errorCode)), transaction, cancellationToken)
                .ConfigureAwait(false);
        }

        return failed;
    }

    public async Task<ResearchJob?> ExpireAsync(ResearchJob job, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        var expired = await FinishAsync(job, JobStatus.Expired, JobsErrors.DeadlineExceeded.Code, [], transaction, cancellationToken)
            .ConfigureAwait(false);
        if (expired is not null)
        {
            await outbox.EnqueueAsync(envelopes.Create(new JobExpired(job.Id, JobStepKey)), transaction, cancellationToken)
                .ConfigureAwait(false);
        }

        return expired;
    }

    public async Task<ResearchJob?> CancelAsync(ResearchJob job, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        var cancelled = await FinishAsync(job, JobStatus.Cancelled, null, [], transaction, cancellationToken).ConfigureAwait(false);
        if (cancelled is not null)
        {
            await outbox.EnqueueAsync(envelopes.Create(new JobCancelled(job.Id, JobStepKey)), transaction, cancellationToken)
                .ConfigureAwait(false);
        }

        return cancelled;
    }

    /// <summary>Moves a synthesizing job to its final state once the report is stored.</summary>
    public async Task<ResearchJob?> CompleteAsync(ResearchJob job, bool reportTruncated, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        var counts = await steps.CountAsync(job.Id, transaction, cancellationToken).ConfigureAwait(false);
        var (status, warnings) = JobOutcomePolicy.Complete(job.GatheringEndReason, counts.GatheringFailed, reportTruncated);

        var completed = await FinishAsync(job, status, null, warnings, transaction, cancellationToken).ConfigureAwait(false);
        if (completed is not null)
        {
            await outbox.EnqueueAsync(
                envelopes.Create(new JobCompleted(job.Id, JobStepKey, status.ToString(), warnings)),
                transaction,
                cancellationToken).ConfigureAwait(false);
        }

        return completed;
    }

    private async Task<ResearchJob?> FinishAsync(
        ResearchJob job,
        JobStatus status,
        string? errorCode,
        IReadOnlyList<string> warnings,
        ITransactionScope transaction,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var finished = await SaveAsync(
            job,
            job with { Status = status, ErrorCode = errorCode, Warnings = warnings, CompletedAt = now },
            transaction,
            cancellationToken).ConfigureAwait(false);

        if (finished is not null)
        {
            metrics.Finished(status, errorCode, now - job.CreatedAt);
            LogJobFinished(logger, job.Id, status, errorCode);
        }

        return finished;
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Job {JobId} transition {From} -> {To} lost a race; nothing changed")]
    private static partial void LogTransitionLost(ILogger logger, Guid jobId, JobStatus from, JobStatus to);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Job {JobId} gathering ended ({Reason}) with {Sources} sources and {FailedSteps} failed steps")]
    private static partial void LogGatheringEnded(ILogger logger, Guid jobId, GatheringEndReason reason, int sources, int failedSteps);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Job {JobId} finished as {Status} {ErrorCode}")]
    private static partial void LogJobFinished(ILogger logger, Guid jobId, JobStatus status, string? errorCode);
}
