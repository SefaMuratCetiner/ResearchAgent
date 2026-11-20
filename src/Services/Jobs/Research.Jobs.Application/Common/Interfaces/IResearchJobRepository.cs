using Research.Jobs.Domain;
using Research.Shared.Core.Transactions;

namespace Research.Jobs.Application.Common.Interfaces;

/// <summary>Persistence of <see cref="ResearchJob"/>. Writes take the caller's transaction.</summary>
public interface IResearchJobRepository
{
    /// <summary>Inserts the job; false when its idempotency key is already taken (the caller re-reads that job).</summary>
    Task<bool> InsertAsync(ResearchJob job, ITransactionScope transaction, CancellationToken cancellationToken);

    Task<ResearchJob?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ResearchJob?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Reads and locks the job row (<c>FOR UPDATE</c>), serializing every handler of the same job.</summary>
    Task<ResearchJob?> GetForUpdateAsync(Guid id, ITransactionScope transaction, CancellationToken cancellationToken);

    /// <summary>Like <see cref="GetForUpdateAsync"/> but skips a row another transaction holds (workers).</summary>
    Task<ResearchJob?> TryLockAsync(Guid id, ITransactionScope transaction, CancellationToken cancellationToken);

    /// <summary>
    /// Compare-and-set: writes <paramref name="job"/> (with version <paramref name="expectedVersion"/> + 1) only if
    /// the row still has <paramref name="expectedVersion"/> and one of <paramref name="allowedFrom"/>.
    /// </summary>
    Task<bool> UpdateAsync(
        ResearchJob job,
        int expectedVersion,
        IReadOnlyList<JobStatus> allowedFrom,
        ITransactionScope transaction,
        CancellationToken cancellationToken);

    /// <summary>Gathering jobs whose synthesis cutoff passed.</summary>
    Task<IReadOnlyList<Guid>> ListPastCutoffAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    /// <summary>Active jobs whose deadline passed.</summary>
    Task<IReadOnlyList<Guid>> ListPastDeadlineAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    /// <summary>Active jobs with at least one running step whose lease expired.</summary>
    Task<IReadOnlyList<Guid>> ListWithExpiredStepsAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    /// <summary>Jobs in an active state (planning, gathering or synthesizing), counted up to <paramref name="limit"/>.</summary>
    Task<int> CountActiveAsync(int limit, CancellationToken cancellationToken);
}
