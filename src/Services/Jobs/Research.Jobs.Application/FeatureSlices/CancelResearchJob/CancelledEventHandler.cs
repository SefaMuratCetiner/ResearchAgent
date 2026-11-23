using Research.Jobs.Application.Common.Services;
using Research.Jobs.Domain;
using Research.Jobs.Domain.Events;
using Research.Shared.Bus.Events;
using Research.Shared.Contracts.Jobs;

namespace Research.Jobs.Application.CancelResearchJob;

/// <summary>Announces a cancelled job (<c>JobCancelled</c>).</summary>
public sealed class CancelledEventHandler(JobTerminationService termination) : IDomainEventHandler<CancelledDomainEvent>
{
    public Task HandleAsync(CancelledDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return termination.FinishAsync(
            new JobCancelled(domainEvent.JobId, JobTerminationService.JobStepKey),
            JobStatus.Cancelled,
            errorCode: null,
            domainEvent.OccurredAt - domainEvent.JobCreatedAt,
            cancellationToken);
    }
}
