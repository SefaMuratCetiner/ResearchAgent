using Research.Jobs.Domain;

namespace Research.Jobs.Application.Common.Interfaces;

/// <summary>Persistence of <see cref="ResearchJob"/>. Writes enlist in the scoped unit of work.</summary>
public interface IResearchJobRepository
{
    /// <summary>Inserts the job; false when its idempotency key is already taken (the caller re-reads that job).</summary>
    Task<bool> InsertAsync(ResearchJob job, CancellationToken cancellationToken);

    Task<ResearchJob?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ResearchJob?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Reads and locks the job row (<c>FOR UPDATE</c>), serializing every handler of the same job.</summary>
    Task<ResearchJob?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Like <see cref="GetForUpdateAsync"/> but skips a row another transaction holds (workers).</summary>
    Task<ResearchJob?> TryLockAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Compare-and-set: writes <paramref name="job"/> (incrementing its persisted version and stamping update audit
    /// fields) only if the row still matches the version and allowed source statuses tracked by the repository.
    /// </summary>
    Task<bool> UpdateAsync(ResearchJob job, CancellationToken cancellationToken);

    /// <summary>Gathering jobs whose synthesis cutoff passed.</summary>
    Task<IReadOnlyList<Guid>> ListPastCutoffAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    /// <summary>Active jobs whose deadline passed.</summary>
    Task<IReadOnlyList<Guid>> ListPastDeadlineAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    /// <summary>Active jobs with at least one running step whose lease expired.</summary>
    Task<IReadOnlyList<Guid>> ListWithExpiredStepsAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    /// <summary>Jobs in an active state (planning, gathering or synthesizing), counted up to <paramref name="limit"/>.</summary>
    Task<int> CountActiveAsync(int limit, CancellationToken cancellationToken);
}
