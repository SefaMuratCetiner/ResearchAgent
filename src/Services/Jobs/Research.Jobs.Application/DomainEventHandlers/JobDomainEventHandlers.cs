using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Contracts.Jobs;
using Research.Contracts.Planning;
using Research.Contracts.Search;
using Research.Contracts.Synthesis;
using Research.Jobs.Domain;
using Research.Jobs.Domain.Events;
using Research.Jobs.Lifecycle;
using Research.Jobs.Observability;
using Research.Shared.Core.Events;
using Research.Shared.Core.Messaging;
using Research.Shared.Core.Transactions;

namespace Research.Jobs.DomainEventHandlers;

/// <summary>Emits the initial <c>PlanRequested</c> step and enqueues <c>JobCreated</c> when a job is created.</summary>
public sealed class JobCreatedDomainEventHandler(
    StepEmitter emitter,
    IOutbox outbox,
    IEnvelopeFactory envelopes,
    JobsMetrics metrics,
    IOptionsSnapshot<JobsOptions> options) : IDomainEventHandler<JobCreatedDomainEvent>
{
    private const string JobStepKey = "job";

    public async Task HandleAsync(JobCreatedDomainEvent domainEvent, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        await emitter.EmitAsync(
            new PlanRequested(domainEvent.JobId, JobStepKeys.Plan, domainEvent.Question, options.Value.MaxSubQuestions),
            transaction,
            cancellationToken).ConfigureAwait(false);
        await outbox.EnqueueAsync(
            envelopes.Create(new JobCreated(domainEvent.JobId, JobStepKey, domainEvent.Question)),
            transaction,
            cancellationToken).ConfigureAwait(false);
        metrics.Created();
    }
}

/// <summary>Emits one <c>SearchRequested</c> step per sub-question when a job enters Gathering.</summary>
public sealed class JobGatheringStartedDomainEventHandler(
    StepEmitter emitter,
    IOptionsSnapshot<JobsOptions> options) : IDomainEventHandler<JobGatheringStartedDomainEvent>
{
    public async Task HandleAsync(JobGatheringStartedDomainEvent domainEvent, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var resultsPerSearch = options.Value.ResultsPerSearch;
        for (var i = 0; i < domainEvent.SubQuestions.Count; i++)
        {
            await emitter.EmitAsync(
                new SearchRequested(domainEvent.JobId, JobStepKeys.Search(i), domainEvent.SubQuestions[i], resultsPerSearch),
                transaction,
                cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>Emits <c>SynthesisRequested</c> when a job enters Synthesizing.</summary>
public sealed class JobSynthesisStartedDomainEventHandler(
    StepEmitter emitter,
    IOptionsSnapshot<JobsOptions> options) : IDomainEventHandler<JobSynthesisStartedDomainEvent>
{
    public Task HandleAsync(JobSynthesisStartedDomainEvent domainEvent, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return emitter.EmitAsync(
            new SynthesisRequested(domainEvent.JobId, JobStepKeys.Synthesize, domainEvent.Question, domainEvent.Plan, options.Value.SynthesisTopK),
            transaction,
            cancellationToken);
    }
}

/// <summary>Enqueues <c>JobCompleted</c> and records completion telemetry.</summary>
public sealed partial class JobCompletedDomainEventHandler(
    IOutbox outbox,
    IEnvelopeFactory envelopes,
    JobsMetrics metrics,
    ILogger<JobCompletedDomainEventHandler> logger) : IDomainEventHandler<JobCompletedDomainEvent>
{
    private const string JobStepKey = "job";

    public async Task HandleAsync(JobCompletedDomainEvent domainEvent, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        await outbox.EnqueueAsync(
            envelopes.Create(new JobCompleted(domainEvent.JobId, JobStepKey, domainEvent.Status.ToString(), domainEvent.Warnings)),
            transaction,
            cancellationToken).ConfigureAwait(false);
        metrics.Finished(domainEvent.Status, errorCode: null, domainEvent.OccurredAt - domainEvent.JobCreatedAt);
        LogJobFinished(logger, domainEvent.JobId, domainEvent.Status, errorCode: null);
    }

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Job {JobId} finished as {Status} {ErrorCode}")]
    private static partial void LogJobFinished(ILogger logger, Guid jobId, JobStatus status, string? errorCode);
}

/// <summary>Enqueues <c>JobCancelled</c> and records cancellation telemetry.</summary>
public sealed partial class JobCancelledDomainEventHandler(
    IOutbox outbox,
    IEnvelopeFactory envelopes,
    JobsMetrics metrics,
    ILogger<JobCancelledDomainEventHandler> logger) : IDomainEventHandler<JobCancelledDomainEvent>
{
    private const string JobStepKey = "job";

    public async Task HandleAsync(JobCancelledDomainEvent domainEvent, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        await outbox.EnqueueAsync(
            envelopes.Create(new JobCancelled(domainEvent.JobId, JobStepKey)),
            transaction,
            cancellationToken).ConfigureAwait(false);
        metrics.Finished(JobStatus.Cancelled, errorCode: null, domainEvent.OccurredAt - domainEvent.JobCreatedAt);
        LogJobFinished(logger, domainEvent.JobId, JobStatus.Cancelled, errorCode: null);
    }

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Job {JobId} finished as {Status} {ErrorCode}")]
    private static partial void LogJobFinished(ILogger logger, Guid jobId, JobStatus status, string? errorCode);
}

/// <summary>Enqueues <c>JobFailed</c> and records failure telemetry.</summary>
public sealed partial class JobFailedDomainEventHandler(
    IOutbox outbox,
    IEnvelopeFactory envelopes,
    JobsMetrics metrics,
    ILogger<JobFailedDomainEventHandler> logger) : IDomainEventHandler<JobFailedDomainEvent>
{
    private const string JobStepKey = "job";

    public async Task HandleAsync(JobFailedDomainEvent domainEvent, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        await outbox.EnqueueAsync(
            envelopes.Create(new JobFailed(domainEvent.JobId, JobStepKey, domainEvent.ErrorCode)),
            transaction,
            cancellationToken).ConfigureAwait(false);
        metrics.Finished(JobStatus.Failed, domainEvent.ErrorCode, domainEvent.OccurredAt - domainEvent.JobCreatedAt);
        LogJobFinished(logger, domainEvent.JobId, JobStatus.Failed, domainEvent.ErrorCode);
    }

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Job {JobId} finished as {Status} {ErrorCode}")]
    private static partial void LogJobFinished(ILogger logger, Guid jobId, JobStatus status, string? errorCode);
}

/// <summary>Enqueues <c>JobExpired</c> and records expiration telemetry.</summary>
public sealed partial class JobExpiredDomainEventHandler(
    IOutbox outbox,
    IEnvelopeFactory envelopes,
    JobsMetrics metrics,
    ILogger<JobExpiredDomainEventHandler> logger) : IDomainEventHandler<JobExpiredDomainEvent>
{
    private const string JobStepKey = "job";

    public async Task HandleAsync(JobExpiredDomainEvent domainEvent, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        await outbox.EnqueueAsync(
            envelopes.Create(new JobExpired(domainEvent.JobId, JobStepKey)),
            transaction,
            cancellationToken).ConfigureAwait(false);
        metrics.Finished(JobStatus.Expired, JobsErrors.DeadlineExceeded.Code, domainEvent.OccurredAt - domainEvent.JobCreatedAt);
        LogJobFinished(logger, domainEvent.JobId, JobStatus.Expired, JobsErrors.DeadlineExceeded.Code);
    }

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Job {JobId} finished as {Status} {ErrorCode}")]
    private static partial void LogJobFinished(ILogger logger, Guid jobId, JobStatus status, string? errorCode);
}
