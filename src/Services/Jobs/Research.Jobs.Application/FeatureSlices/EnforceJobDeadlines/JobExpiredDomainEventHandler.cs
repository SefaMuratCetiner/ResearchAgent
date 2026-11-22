using Microsoft.Extensions.Logging;
using Research.Contracts.Jobs;
using Research.Jobs.Application.Observability;
using Research.Jobs.Domain;
using Research.Jobs.Domain.Events;
using Research.Shared.Core.Events;
using Research.Shared.Messaging;

namespace Research.Jobs.Application.EnforceJobDeadlines;

/// <summary>Enqueues <c>JobExpired</c> and records expiration telemetry.</summary>
public sealed partial class JobExpiredDomainEventHandler(
    IOutbox outbox,
    IEnvelopeFactory envelopes,
    JobsMetrics metrics,
    ILogger<JobExpiredDomainEventHandler> logger) : IDomainEventHandler<JobExpiredDomainEvent>
{
    private const string JobStepKey = "job";

    public async Task HandleAsync(JobExpiredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        await outbox.EnqueueAsync(
            envelopes.Create(new JobExpired(domainEvent.JobId, JobStepKey)),
            cancellationToken).ConfigureAwait(false);
        metrics.Finished(JobStatus.Expired, JobsErrors.DeadlineExceeded.Code, domainEvent.OccurredAt - domainEvent.JobCreatedAt);
        LogJobFinished(logger, domainEvent.JobId, JobStatus.Expired, JobsErrors.DeadlineExceeded.Code);
    }

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Job {JobId} finished as {Status} {ErrorCode}")]
    private static partial void LogJobFinished(ILogger logger, Guid jobId, JobStatus status, string? errorCode);
}
