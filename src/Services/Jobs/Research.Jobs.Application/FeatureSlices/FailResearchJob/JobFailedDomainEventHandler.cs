using Microsoft.Extensions.Logging;
using Research.Contracts.Jobs;
using Research.Jobs.Application.Observability;
using Research.Jobs.Domain;
using Research.Jobs.Domain.Events;
using Research.Shared.Core.Events;
using Research.Shared.Messaging;

namespace Research.Jobs.Application.FailResearchJob;

/// <summary>Enqueues <c>JobFailed</c> and records failure telemetry.</summary>
public sealed partial class JobFailedDomainEventHandler(
    IOutbox outbox,
    IEnvelopeFactory envelopes,
    JobsMetrics metrics,
    ILogger<JobFailedDomainEventHandler> logger) : IDomainEventHandler<JobFailedDomainEvent>
{
    private const string JobStepKey = "job";

    public async Task HandleAsync(JobFailedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        await outbox.EnqueueAsync(
            envelopes.Create(new JobFailed(domainEvent.JobId, JobStepKey, domainEvent.ErrorCode)),
            cancellationToken).ConfigureAwait(false);
        metrics.Finished(JobStatus.Failed, domainEvent.ErrorCode, domainEvent.OccurredAt - domainEvent.JobCreatedAt);
        LogJobFinished(logger, domainEvent.JobId, JobStatus.Failed, domainEvent.ErrorCode);
    }

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Job {JobId} finished as {Status} {ErrorCode}")]
    private static partial void LogJobFinished(ILogger logger, Guid jobId, JobStatus status, string? errorCode);
}
