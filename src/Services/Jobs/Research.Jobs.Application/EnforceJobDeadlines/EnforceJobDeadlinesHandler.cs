using Microsoft.Extensions.Options;
using Research.Jobs.Domain;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Lifecycle;
using Research.Shared.Core.Transactions;

namespace Research.Jobs.EnforceJobDeadlines;

/// <summary>
/// Drives the two clocks (§10.1): a gathering job past <c>synthesis_cutoff_at</c> ends gathering
/// (<c>jobs.cutoff_reached</c>); an active job past <c>deadline_at</c> becomes <c>Expired</c>. Both are ordinary
/// compare-and-set transitions under the job's row lock (<c>SKIP LOCKED</c>), so they race safely with replies.
/// </summary>
public sealed class EnforceJobDeadlinesHandler(
    ITransactionFactory transactions,
    IResearchJobRepository jobs,
    JobLifecycle lifecycle,
    IOptionsSnapshot<JobsOptions> options,
    TimeProvider timeProvider)
{
    /// <returns>How many jobs changed state.</returns>
    public async Task<int> HandleAsync(CancellationToken cancellationToken)
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
                (job, transaction) => lifecycle.ExpireAsync(job, transaction, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var jobId in await jobs.ListPastCutoffAsync(now, batch, cancellationToken).ConfigureAwait(false))
        {
            changed += await WithLockedJobAsync(
                jobId,
                job => job.Status != JobStatus.Gathering || job.SynthesisCutoffAt > now,
                (job, transaction) => lifecycle.EvaluateGatheringAsync(job, GatheringEndReason.CutoffReached, transaction, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        return changed;
    }

    private async Task<int> WithLockedJobAsync(
        Guid jobId,
        Func<ResearchJob, bool> skip,
        Func<ResearchJob, ITransactionScope, Task<bool>> change,
        CancellationToken cancellationToken)
    {
        await using var transaction = await transactions.BeginAsync(cancellationToken).ConfigureAwait(false);
        var job = await jobs.TryLockAsync(jobId, transaction, cancellationToken).ConfigureAwait(false);
        if (job is null || skip(job))
        {
            return 0;
        }

        var changed = await change(job, transaction).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return changed ? 1 : 0;
    }
}
