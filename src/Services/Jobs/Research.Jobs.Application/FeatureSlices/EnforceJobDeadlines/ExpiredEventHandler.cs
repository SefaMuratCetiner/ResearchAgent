using Research.Jobs.Application.Common.Services;
using Research.Jobs.Domain;
using Research.Jobs.Domain.Events;
using Research.Shared.Bus.Events;
using Research.Shared.Contracts.Jobs;

namespace Research.Jobs.Application.EnforceJobDeadlines;

/// <summary>Announces an expired job (<c>JobExpired</c>).</summary>
public sealed class ExpiredEventHandler(JobTerminationService termination) : IDomainEventHandler<ExpiredDomainEvent>
{
    public Task HandleAsync(ExpiredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return termination.FinishAsync(
            new JobExpired(domainEvent.JobId, JobTerminationService.JobStepKey),
            JobStatus.Expired,
            JobsErrors.DeadlineExceeded.Code,
            domainEvent.OccurredAt - domainEvent.JobCreatedAt,
            cancellationToken);
    }
}
