using Research.Jobs.Application.Common.Services;
using Research.Jobs.Domain.Events;
using Research.Shared.Bus.Events;
using Research.Shared.Contracts.Jobs;

namespace Research.Jobs.Application.CompleteOnReportReady;

/// <summary>Announces a completed job (<c>JobCompleted</c>).</summary>
public sealed class CompletedEventHandler(JobTerminationService termination) : IDomainEventHandler<CompletedDomainEvent>
{
    public Task HandleAsync(CompletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return termination.FinishAsync(
            new JobCompleted(domainEvent.JobId, JobTerminationService.JobStepKey, domainEvent.Status.ToString(), domainEvent.Warnings),
            domainEvent.Status,
            errorCode: null,
            domainEvent.OccurredAt - domainEvent.JobCreatedAt,
            cancellationToken);
    }
}
