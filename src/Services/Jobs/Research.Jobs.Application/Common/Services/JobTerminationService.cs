using Microsoft.Extensions.Logging;
using Research.Jobs.Application.Observability;
using Research.Jobs.Domain;
using Research.Shared.Bus.Messaging;
using Research.Shared.Contracts;

namespace Research.Jobs.Application.Common.Services;

/// <summary>
/// Announces that a job reached a terminal state: enqueues its notification in the outbox and records the outcome in
/// metrics and logs.
/// </summary>
public sealed class JobTerminationService(
    IOutbox outbox,
    IEnvelopeFactory envelopes,
    JobsMetrics metrics,
    ILogger<JobTerminationService> logger)
{
    /// <summary>The step key job-level notifications carry.</summary>
    public const string JobStepKey = "job";

    public async Task FinishAsync<TNotification>(
        TNotification notification,
        JobStatus status,
        string? errorCode,
        TimeSpan duration,
        CancellationToken cancellationToken)
        where TNotification : IIntegrationMessage
    {
        ArgumentNullException.ThrowIfNull(notification);
        await outbox.EnqueueAsync(envelopes.Create(notification), cancellationToken).ConfigureAwait(false);
        metrics.Finished(status, errorCode, duration);
        JobsLog.JobFinished(logger, notification.JobId, status, errorCode);
    }
}
