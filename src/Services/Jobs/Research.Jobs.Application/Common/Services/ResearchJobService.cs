using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Domain;
using Research.Shared.Bus.Events;

namespace Research.Jobs.Application.Common.Services;

/// <summary>
/// The one place a <see cref="ResearchJob"/> is written. After a successful insert or compare-and-set update it
/// publishes the job's pending domain events, so their handlers enqueue the follow-up steps and notifications in the
/// same transaction.
/// </summary>
public sealed class ResearchJobService(IResearchJobRepository jobs, IDomainEventDispatcher events)
{
    /// <returns><see langword="false"/> when the idempotency key is already taken; nothing is published then.</returns>
    public async Task<bool> CreateAsync(ResearchJob job, CancellationToken cancellationToken) =>
        await jobs.InsertAsync(job, cancellationToken).ConfigureAwait(false)
        && await PublishAsync(job, cancellationToken).ConfigureAwait(false);

    /// <returns><see langword="false"/> when the compare-and-set lost; nothing is published then.</returns>
    public async Task<bool> SaveAsync(ResearchJob job, CancellationToken cancellationToken) =>
        await jobs.UpdateAsync(job, cancellationToken).ConfigureAwait(false)
        && await PublishAsync(job, cancellationToken).ConfigureAwait(false);

    private async Task<bool> PublishAsync(ResearchJob job, CancellationToken cancellationToken)
    {
        var raised = job.DomainEvents.ToList();
        job.ClearDomainEvents();
        await events.DispatchAsync(raised, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
