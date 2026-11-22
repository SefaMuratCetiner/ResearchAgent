using Microsoft.Extensions.Logging;
using Research.Contracts.Jobs;
using Research.Jobs.Application.Observability;
using Research.Jobs.Domain;
using Research.Jobs.Domain.Events;
using Research.Shared.Core.Events;
using Research.Shared.Messaging;

namespace Research.Jobs.Application.CompleteOnReportReady;

/// <summary>Enqueues <c>JobCompleted</c> and records completion telemetry.</summary>
public sealed partial class JobCompletedDomainEventHandler(
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
        LogJobFinished(logger, domainEvent.JobId, domainEvent.Status, errorCode: null);
    }

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Job {JobId} finished as {Status} {ErrorCode}")]
    private static partial void LogJobFinished(ILogger logger, Guid jobId, JobStatus status, string? errorCode);
}
