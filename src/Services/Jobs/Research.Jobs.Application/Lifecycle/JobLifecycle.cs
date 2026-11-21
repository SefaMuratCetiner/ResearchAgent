using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Contracts.Jobs;
using Research.Contracts.Synthesis;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Domain;
using Research.Jobs.Observability;
using Research.Shared.Core.Actors;
using Research.Shared.Core.Messaging;
using Research.Shared.Core.Transactions;
using Research.SharedKernel.Results;

namespace Research.Jobs.Lifecycle;

/// <summary>
/// Persists <see cref="ResearchJob"/> aggregate transitions with compare-and-set updates and enqueues outgoing
/// integration messages in the caller's transaction. Domain invariants live on <see cref="ResearchJob"/> itself.
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

    /// <summary>Saves pending mutations on <paramref name="job"/> if the database row still matches its persisted state.</summary>
    public async Task<bool> SaveAsync(ResearchJob job, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        var expectedVersion = job.PersistedVersion;
        var fromStatus = job.PersistedStatus;
        var allowedFrom = job.GetAllowedSourceStatuses();
        job.PrepareForUpdate(timeProvider.GetUtcNow(), actor.Id);

        var saved = await jobs.UpdateAsync(job, expectedVersion, allowedFrom, transaction, cancellationToken).ConfigureAwait(false);
        if (!saved)
        {
            LogTransitionLost(logger, job.Id, fromStatus, job.Status);
            return false;
        }

        job.AcceptPersistedState();
        return true;
    }

    /// <summary>Records billed tokens on <paramref name="job"/> and saves it.</summary>
    public Task<bool> AddTokensAsync(ResearchJob job, long tokens, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        return !job.RecordTokens(tokens)
            ? Task.FromResult(true)
            : SaveAsync(job, transaction, cancellationToken);
    }

    /// <summary>
    /// Ends gathering when it is over (§10.1): every search and fetch step finished, or <paramref name="forcedReason"/>
    /// (cutoff, budget). Delegates the decision to <see cref="ResearchJob.EndGathering"/> and emits
    /// <c>SynthesisRequested</c> or <c>JobFailed</c> in the caller's transaction.
    /// </summary>
    public async Task<bool> EvaluateGatheringAsync(
        ResearchJob job,
        GatheringEndReason? forcedReason,
        ITransactionScope transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.Status != JobStatus.Gathering)
        {
            return false;
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
            return false;
        }

        metrics.GatheringEnded(counts.FetchSucceeded);
        LogGatheringEnded(logger, job.Id, reason.Value, counts.FetchSucceeded, counts.GatheringFailed);

        var now = timeProvider.GetUtcNow();
        if (!job.EndGathering(reason.Value, counts.FetchSucceeded, options.Value.MinSources, now))
        {
            return false;
        }

        if (!await SaveAsync(job, transaction, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (job.Status == JobStatus.Failed)
        {
            RecordFinished(job, now);
            await outbox.EnqueueAsync(envelopes.Create(new JobFailed(job.Id, JobStepKey, job.ErrorCode!)), transaction, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        await emitter.EmitAsync(
            new SynthesisRequested(job.Id, JobStepKeys.Synthesize, job.Question, job.Plan, options.Value.SynthesisTopK),
            transaction,
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> FailAsync(ResearchJob job, string errorCode, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        var now = timeProvider.GetUtcNow();
        if (!job.Fail(errorCode, now) || !await SaveAsync(job, transaction, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        RecordFinished(job, now);
        await outbox.EnqueueAsync(envelopes.Create(new JobFailed(job.Id, JobStepKey, errorCode)), transaction, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    public async Task<bool> ExpireAsync(ResearchJob job, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        var now = timeProvider.GetUtcNow();
        if (!job.Expire(now) || !await SaveAsync(job, transaction, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        RecordFinished(job, now);
        await outbox.EnqueueAsync(envelopes.Create(new JobExpired(job.Id, JobStepKey)), transaction, cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    public async Task<Result<ResearchJob>> CancelAsync(ResearchJob job, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        var wasCancelled = job.Status == JobStatus.Cancelled;
        var now = timeProvider.GetUtcNow();
        var decision = job.Cancel(now);
        if (!decision.IsSuccess)
        {
            return Result<ResearchJob>.Failure(decision.Error);
        }

        if (wasCancelled)
        {
            return Result<ResearchJob>.Success(job);
        }

        if (!await SaveAsync(job, transaction, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The cancel transition failed although the job row is locked.");
        }

        RecordFinished(job, now);
        await outbox.EnqueueAsync(envelopes.Create(new JobCancelled(job.Id, JobStepKey)), transaction, cancellationToken)
            .ConfigureAwait(false);
        return Result<ResearchJob>.Success(job);
    }

    /// <summary>Moves a synthesizing job to its final state once the report is stored.</summary>
    public async Task<bool> CompleteAsync(ResearchJob job, bool reportTruncated, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        var counts = await steps.CountAsync(job.Id, transaction, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        if (!job.Complete(counts.GatheringFailed, reportTruncated, now)
            || !await SaveAsync(job, transaction, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        RecordFinished(job, now);
        await outbox.EnqueueAsync(
            envelopes.Create(new JobCompleted(job.Id, JobStepKey, job.Status.ToString(), job.Warnings)),
            transaction,
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    private void RecordFinished(ResearchJob job, DateTimeOffset now)
    {
        metrics.Finished(job.Status, job.ErrorCode, now - job.CreatedAt);
        LogJobFinished(logger, job.Id, job.Status, job.ErrorCode);
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Job {JobId} transition {From} -> {To} lost a race; nothing changed")]
    private static partial void LogTransitionLost(ILogger logger, Guid jobId, JobStatus from, JobStatus to);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Job {JobId} gathering ended ({Reason}) with {Sources} sources and {FailedSteps} failed steps")]
    private static partial void LogGatheringEnded(ILogger logger, Guid jobId, GatheringEndReason reason, int sources, int failedSteps);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Job {JobId} finished as {Status} {ErrorCode}")]
    private static partial void LogJobFinished(ILogger logger, Guid jobId, JobStatus status, string? errorCode);
}
