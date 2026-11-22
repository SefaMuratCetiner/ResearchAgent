using Microsoft.Extensions.Logging;
using Research.Contracts.Jobs;
using Research.Jobs.Application.Observability;
using Research.Jobs.Domain;
using Research.Jobs.Domain.Events;
using Research.Shared.Core.Events;
using Research.Shared.Messaging;

namespace Research.Jobs.Application.CancelResearchJob;

/// <summary>Enqueues <c>JobCancelled</c> and records cancellation telemetry.</summary>
public sealed class JobCancelledDomainEventHandler(
    IOutbox outbox,
    IEnvelopeFactory envelopes,
    JobsMetrics metrics,
    ILogger<JobCancelledDomainEventHandler> logger) : IDomainEventHandler<JobCancelledDomainEvent>
{
    private const string JobStepKey = "job";

    public async Task HandleAsync(JobCancelledDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        await outbox.EnqueueAsync(
            envelopes.Create(new JobCancelled(domainEvent.JobId, JobStepKey)),
            cancellationToken).ConfigureAwait(false);
        metrics.Finished(JobStatus.Cancelled, errorCode: null, domainEvent.OccurredAt - domainEvent.JobCreatedAt);
        JobsLog.JobFinished(logger, domainEvent.JobId, JobStatus.Cancelled, errorCode: null);
    }
}
