using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Application.Common.Services;
using Research.Jobs.Domain;
using Research.Shared.Bus.Transactions;

namespace Research.Jobs.Application.EnforceJobDeadlines;

/// <summary>
/// Drives the two clocks (§10.1): a gathering job past <c>synthesis_cutoff_at</c> ends gathering
/// (<c>jobs.cutoff_reached</c>); an active job past <c>deadline_at</c> becomes <c>Expired</c>. Both are ordinary
/// compare-and-set transitions under the job's row lock (<c>SKIP LOCKED</c>), so they race safely with replies.
/// </summary>
public sealed class JobDeadlineService(
    IUnitOfWork unitOfWork,
    IResearchJobRepository jobs,
    ResearchJobService jobService,
    GatheringPhaseCompleter gatheringCompleter,
    IOptionsSnapshot<JobsOptions> options,
    TimeProvider timeProvider,
    ILogger<JobDeadlineService> logger)
{
    /// <returns>How many jobs changed state.</returns>
    public async Task<int> EnforceAsync(CancellationToken cancellationToken)
    {
        var batch = options.Value.WorkerBatchSize;
        var now = timeProvider.GetUtcNow();
        var changed = 0;

        // Deadlines first: a job past both clocks expires instead of starting a synthesis it has no time for.
        foreach (var jobId in await jobs.ListPastDeadlineAsync(now, batch, cancellationToken).ConfigureAwait(false))
        {
            changed += await WithLockedJobAsync(
                jobId,
                job => job.Status.IsTerminal() || job.DeadlineAt > now,
                job => Task.FromResult(job.Expire(now)),
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var jobId in await jobs.ListPastCutoffAsync(now, batch, cancellationToken).ConfigureAwait(false))
        {
            changed += await WithLockedJobAsync(
                jobId,
                job => job.Status != JobStatus.Gathering || job.SynthesisCutoffAt > now,
                job => gatheringCompleter.TryCompleteAsync(job, GatheringEndReason.CutoffReached, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        return changed;
    }

    private async Task<int> WithLockedJobAsync(
        Guid jobId,
        Func<ResearchJob, bool> skip,
        Func<ResearchJob, Task<bool>> change,
        CancellationToken cancellationToken)
    {
        using var jobScope = logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal) { ["JobId"] = jobId });
        await using var _ = await unitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);
        var job = await jobs.TryLockAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null || skip(job))
        {
            return 0;
        }

        var changed = await change(job).ConfigureAwait(false)
            && await jobService.SaveAsync(job, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return changed ? 1 : 0;
    }
}
