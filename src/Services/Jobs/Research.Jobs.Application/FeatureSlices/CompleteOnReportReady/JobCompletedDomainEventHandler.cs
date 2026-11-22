using Microsoft.Extensions.Logging;
using Research.Shared.Contracts.Jobs;
using Research.Jobs.Application.Observability;
using Research.Jobs.Domain.Events;
using Research.Shared.Core.Events;
using Research.Shared.Messaging;

namespace Research.Jobs.Application.CompleteOnReportReady;

/// <summary>Enqueues <c>JobCompleted</c> and records completion telemetry.</summary>
public sealed class JobCompletedDomainEventHandler(
    IOutbox outbox,
    IEnvelopeFactory envelopes,
    JobsMetrics metrics,
    ILogger<JobCompletedDomainEventHandler> logger) : IDomainEventHandler<JobCompletedDomainEvent>
{
    private const string JobStepKey = "job";

    public async Task HandleAsync(JobCompletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        await outbox.EnqueueAsync(
            envelopes.Create(new JobCompleted(domainEvent.JobId, JobStepKey, domainEvent.Status.ToString(), domainEvent.Warnings)),
            cancellationToken).ConfigureAwait(false);
        metrics.Finished(domainEvent.Status, errorCode: null, domainEvent.OccurredAt - domainEvent.JobCreatedAt);
        JobsLog.JobFinished(logger, domainEvent.JobId, domainEvent.Status, errorCode: null);
    }
}
