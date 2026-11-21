using Research.Jobs.Domain.Events;
using Research.SharedKernel.Entities;
using Research.SharedKernel.Results;

namespace Research.Jobs.Domain;

/// <summary>
/// The research job aggregate root. Owns its state transitions, cancellation and outcome invariants, and raises
/// domain events on every lifecycle transition. Persisted via compare-and-set on <see cref="PersistedVersion"/> and
/// <see cref="GetAllowedSourceStatuses"/>.
/// </summary>
public sealed class ResearchJob : AggregateRoot
{
    private ResearchJob()
    {
    }

    public string Question { get; private set; } = string.Empty;

    public JobStatus Status { get; private set; }

    /// <summary>The sub-questions from Planning; empty until the plan arrives.</summary>
    public IReadOnlyList<string> Plan { get; private set; } = [];

    public string? IdempotencyKey { get; private set; }

    /// <summary>When gathering ends regardless of open steps.</summary>
    public DateTimeOffset SynthesisCutoffAt { get; private set; }

    /// <summary>When an active job expires.</summary>
    public DateTimeOffset DeadlineAt { get; private set; }

    public long TokenBudget { get; private set; }

    public long TokensUsed { get; private set; }

    /// <summary>Set on <see cref="JobStatus.Failed"/> and <see cref="JobStatus.Expired"/>.</summary>
    public string? ErrorCode { get; private set; }

    /// <summary>Warning codes of <see cref="JobStatus.CompletedWithWarnings"/>.</summary>
    public IReadOnlyList<string> Warnings { get; private set; } = [];

    public GatheringEndReason? GatheringEndReason { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public int Version { get; private set; }

    /// <summary>The version currently persisted in the database, used as the CAS precondition.</summary>
    public int PersistedVersion { get; private set; }

    /// <summary>The status currently persisted in the database, used to compute the CAS allowed source states.</summary>
    public JobStatus PersistedStatus { get; private set; }

    public bool BudgetExhausted => TokensUsed >= TokenBudget;

    /// <summary>Creates a new job in <see cref="JobStatus.Planning"/> with both clocks set and raises <see cref="JobCreatedDomainEvent"/>.</summary>
    public static ResearchJob Create(
        Guid id,
        string question,
        string? idempotencyKey,
        TimeSpan maxDuration,
        TimeSpan synthesisReserve,
        long tokenBudget,
        DateTimeOffset now,
        string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tokenBudget);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxDuration, TimeSpan.Zero);
        if (synthesisReserve < TimeSpan.Zero || synthesisReserve >= maxDuration)
        {
            throw new ArgumentOutOfRangeException(nameof(synthesisReserve), "Synthesis reserve must be non-negative and shorter than max duration.");
        }

        var job = new ResearchJob
        {
            Id = id,
            Question = question,
            Status = JobStatus.Planning,
            IdempotencyKey = idempotencyKey,
            SynthesisCutoffAt = now + maxDuration - synthesisReserve,
            DeadlineAt = now + maxDuration,
            TokenBudget = tokenBudget,
            Version = 0,
            PersistedVersion = 0,
            PersistedStatus = JobStatus.Planning,
            CreatedAt = now,
        };
        if (!string.IsNullOrWhiteSpace(createdBy))
        {
            job.StampCreated(now, createdBy);
        }

        job.Raise(new JobCreatedDomainEvent(id, question, now));
        return job;
    }

    /// <summary>Reconstructs an existing job loaded from persistence without raising domain events.</summary>
    public static ResearchJob Rehydrate(
        Guid id,
        string question,
        JobStatus status,
        IReadOnlyList<string> plan,
        string? idempotencyKey,
        DateTimeOffset synthesisCutoffAt,
        DateTimeOffset deadlineAt,
        long tokenBudget,
        long tokensUsed,
        string? errorCode,
        IReadOnlyList<string> warnings,
        GatheringEndReason? gatheringEndReason,
        DateTimeOffset? completedAt,
        int version,
        DateTimeOffset createdAt,
        string createdBy,
        DateTimeOffset? updatedAt,
        string? updatedBy)
    {
        var job = new ResearchJob
        {
            Id = id,
            Question = question,
            Status = status,
            Plan = plan,
            IdempotencyKey = idempotencyKey,
            SynthesisCutoffAt = synthesisCutoffAt,
            DeadlineAt = deadlineAt,
            TokenBudget = tokenBudget,
            TokensUsed = tokensUsed,
            ErrorCode = errorCode,
            Warnings = warnings,
            GatheringEndReason = gatheringEndReason,
            CompletedAt = completedAt,
            Version = version,
            PersistedVersion = version,
            PersistedStatus = status,
            CreatedAt = createdAt,
            CreatedBy = createdBy,
            UpdatedAt = updatedAt,
            UpdatedBy = updatedBy,
        };
        return job;
    }

    /// <summary>Adds billed tokens to the job; non-positive values are ignored.</summary>
    public bool RecordTokens(long tokens)
    {
        if (tokens <= 0)
        {
            return false;
        }

        TokensUsed += tokens;
        return true;
    }

    /// <summary>
    /// Moves a <see cref="JobStatus.Planning"/> job to <see cref="JobStatus.Gathering"/> with the normalized plan,
    /// or fails it with <see cref="JobsErrors.PlanEmpty"/> when no sub-questions remain.
    /// </summary>
    /// <returns>True when the job moved to <see cref="JobStatus.Gathering"/>; false if empty (failed) or not in Planning.</returns>
    public bool StartGathering(IReadOnlyList<string> subQuestions, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(subQuestions);
        if (Status != JobStatus.Planning)
        {
            return false;
        }

        if (subQuestions.Count == 0)
        {
            Fail(JobsErrors.PlanEmpty.Code, now);
            return false;
        }

        Status = JobStatus.Gathering;
        Plan = [.. subQuestions];
        Raise(new JobGatheringStartedDomainEvent(Id, Plan, now));
        return true;
    }

    /// <summary>
    /// Ends gathering using <see cref="JobOutcomePolicy.EvaluateSources"/>: either fails the job when too few sources
    /// were indexed, or moves it to <see cref="JobStatus.Synthesizing"/>.
    /// </summary>
    /// <returns>True when the job changed state; false when it was not in <see cref="JobStatus.Gathering"/>.</returns>
    public bool EndGathering(GatheringEndReason reason, int indexedSources, int minSources, DateTimeOffset now)
    {
        if (Status != JobStatus.Gathering)
        {
            return false;
        }

        GatheringEndReason = reason;
        var failure = JobOutcomePolicy.EvaluateSources(indexedSources, minSources);
        if (failure is not null)
        {
            return Fail(failure.Code, now);
        }

        Status = JobStatus.Synthesizing;
        Raise(new JobSynthesisStartedDomainEvent(Id, Question, Plan, now));
        return true;
    }

    /// <summary>
    /// Cancels an active job. Cancelling an already-cancelled job is idempotent (succeeds with no new event);
    /// completed jobs return <see cref="JobsErrors.JobAlreadyCompleted"/>; failed or expired jobs return
    /// <see cref="JobsErrors.JobNotCancellable"/>.
    /// </summary>
    public Result Cancel(DateTimeOffset now)
    {
        switch (Status)
        {
            case JobStatus.Cancelled:
                return Result.Success();
            case JobStatus.Completed or JobStatus.CompletedWithWarnings:
                return Result.Failure(JobsErrors.JobAlreadyCompleted);
            case JobStatus.Failed or JobStatus.Expired:
                return Result.Failure(JobsErrors.JobNotCancellable);
        }

        Status = JobStatus.Cancelled;
        ErrorCode = null;
        Warnings = [];
        CompletedAt = now;
        Raise(new JobCancelledDomainEvent(Id, CreatedAt, now));
        return Result.Success();
    }

    /// <summary>Moves a synthesizing job to <see cref="JobStatus.Completed"/> or <see cref="JobStatus.CompletedWithWarnings"/>.</summary>
    public bool Complete(int failedSteps, bool reportTruncated, DateTimeOffset now)
    {
        var (status, warnings) = JobOutcomePolicy.Complete(GatheringEndReason, failedSteps, reportTruncated);
        if (!JobStateMachine.CanMove(Status, status))
        {
            return false;
        }

        Status = status;
        ErrorCode = null;
        Warnings = warnings;
        CompletedAt = now;
        Raise(new JobCompletedDomainEvent(Id, status, warnings, CreatedAt, now));
        return true;
    }

    /// <summary>Moves an active job to <see cref="JobStatus.Failed"/> with <paramref name="errorCode"/>.</summary>
    public bool Fail(string errorCode, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        if (!JobStateMachine.CanMove(Status, JobStatus.Failed))
        {
            return false;
        }

        Status = JobStatus.Failed;
        ErrorCode = errorCode;
        Warnings = [];
        CompletedAt = now;
        Raise(new JobFailedDomainEvent(Id, errorCode, CreatedAt, now));
        return true;
    }

    /// <summary>Moves an active job to <see cref="JobStatus.Expired"/>.</summary>
    public bool Expire(DateTimeOffset now)
    {
        if (!JobStateMachine.CanMove(Status, JobStatus.Expired))
        {
            return false;
        }

        Status = JobStatus.Expired;
        ErrorCode = JobsErrors.DeadlineExceeded.Code;
        Warnings = [];
        CompletedAt = now;
        Raise(new JobExpiredDomainEvent(Id, CreatedAt, now));
        return true;
    }

    /// <summary>The source states the database row must be in for the pending update to succeed.</summary>
    public IReadOnlyList<JobStatus> GetAllowedSourceStatuses() =>
        Status == PersistedStatus ? [PersistedStatus] : JobStateMachine.AllowedFrom(Status);

    /// <summary>Stamps the next version and update audit fields right before writing to persistence.</summary>
    public void PrepareForUpdate(DateTimeOffset now, string actor)
    {
        Version = PersistedVersion + 1;
        StampUpdated(now, actor);
    }

    /// <summary>Marks the current <see cref="Version"/> and <see cref="Status"/> as committed to persistence.</summary>
    public void AcceptPersistedState()
    {
        PersistedVersion = Version;
        PersistedStatus = Status;
    }
}
