using Research.Jobs.Application.Common.Services;
using Research.Jobs.Domain;
using Research.Jobs.Domain.Events;
using Research.Shared.Bus.Events;
using Research.Shared.Contracts.Jobs;

namespace Research.Jobs.Application.FailResearchJob;

/// <summary>Announces a failed job (<c>JobFailed</c>).</summary>
public sealed class FailedEventHandler(JobTerminationService termination) : IDomainEventHandler<FailedDomainEvent>
{
    public Task HandleAsync(FailedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return termination.FinishAsync(
            new JobFailed(domainEvent.JobId, JobTerminationService.JobStepKey, domainEvent.ErrorCode),
            JobStatus.Failed,
            domainEvent.ErrorCode,
            domainEvent.OccurredAt - domainEvent.JobCreatedAt,
            cancellationToken);
    }
}
