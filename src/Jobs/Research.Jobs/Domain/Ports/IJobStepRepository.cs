using Research.SharedKernel.Transactions;

namespace Research.Jobs.Domain.Ports;

/// <summary>Persistence of <see cref="JobStep"/>. Callers hold the job's row lock, so step changes never race.</summary>
public interface IJobStepRepository
{
    /// <summary>Inserts the step; false when the job already has a step with that key (a duplicate request).</summary>
    Task<bool> InsertAsync(JobStep step, ITransactionScope transaction, CancellationToken cancellationToken);

    /// <summary>
    /// Moves a running step to <paramref name="status"/>; false when the step is unknown or already finished
    /// (a duplicate or late reply).
    /// </summary>
    Task<bool> FinishAsync(
        Guid jobId,
        string stepKey,
        StepStatus status,
        string? errorCode,
        DateTimeOffset now,
        string actor,
        ITransactionScope transaction,
        CancellationToken cancellationToken);

    Task<StepCounts> CountAsync(Guid jobId, ITransactionScope transaction, CancellationToken cancellationToken);

    Task<IReadOnlyList<JobStep>> ListAsync(Guid jobId, CancellationToken cancellationToken);

    /// <summary>Running steps of the job whose lease ended before <paramref name="now"/>.</summary>
    Task<IReadOnlyList<JobStep>> ListExpiredAsync(Guid jobId, DateTimeOffset now, ITransactionScope transaction, CancellationToken cancellationToken);

    /// <summary>Records a re-emission: attempts and a new lease.</summary>
    Task RenewLeaseAsync(
        Guid stepId,
        int attempts,
        DateTimeOffset leaseUntil,
        DateTimeOffset now,
        string actor,
        ITransactionScope transaction,
        CancellationToken cancellationToken);
}

/// <summary>A job's steps by state, for the gathering decision.</summary>
/// <param name="GatheringRunning">Search and fetch steps still running.</param>
/// <param name="FetchSucceeded">Fetch steps whose document was indexed: the job's sources.</param>
/// <param name="GatheringFailed">Search and fetch steps that failed.</param>
/// <param name="FetchTotal">All fetch steps, for the <c>MaxSources</c> cap.</param>
public sealed record StepCounts(int GatheringRunning, int FetchSucceeded, int GatheringFailed, int FetchTotal);
