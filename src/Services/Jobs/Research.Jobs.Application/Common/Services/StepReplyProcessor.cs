using Microsoft.Extensions.Logging;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Application.Observability;
using Research.Jobs.Domain;
using Research.Shared.Bus.Transactions;
using Research.Shared.Bus.Messaging;
using Research.Shared.Contracts;
using Research.Shared.Contracts.Steps;

namespace Research.Jobs.Application.Common.Services;

/// <summary>
/// Executes a worker reply handler inside a single idempotent transaction: records the message in the inbox, locks the
/// target <see cref="ResearchJob"/> row, marks the corresponding <see cref="JobStep"/> finished, invokes the slice's
/// state-transition callback, and saves the job once through <see cref="ResearchJobService"/>.
/// </summary>
/// <remarks>
/// Duplicate deliveries, replies for unknown or already-terminal jobs (late results), and replies for already-finished
/// steps are safely ignored and committed without invoking the callback.
/// </remarks>
public sealed partial class StepReplyProcessor(
    IUnitOfWork unitOfWork,
    IInbox inbox,
    IResearchJobRepository jobs,
    IJobStepRepository steps,
    ResearchJobService jobService,
    JobsMetrics metrics,
    ILogger<StepReplyProcessor> logger)
{
    /// <summary>
    /// Processes a single step reply atomically: when the reply is new and finishes its step, invokes
    /// <paramref name="onStepFinished"/> with the locked job, then saves the job and publishes its domain events.
    /// A <see cref="StepFailed"/> reply finishes its step as failed with the worker's code; any other reply as succeeded.
    /// </summary>
    public async Task ProcessAsync<TReply>(
        TReply reply,
        MessageContext context,
        Func<ResearchJob, Task> onStepFinished,
        CancellationToken cancellationToken)
        where TReply : IIntegrationMessage
    {
        ArgumentNullException.ThrowIfNull(reply);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(onStepFinished);
        var (jobId, stepKey) = (reply.JobId, reply.StepKey);
        var (stepStatus, stepErrorCode) = reply is StepFailed failed
            ? (StepStatus.Failed, failed.ErrorCode)
            : (StepStatus.Succeeded, (string?)null);

        await using var _ = await unitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (!await inbox.TryRecordAsync(context.MessageId, context.Consumer, cancellationToken).ConfigureAwait(false))
        {
            LogDuplicate(logger, context.MessageId, context.MessageType);
            return;
        }

        var job = await jobs.GetForUpdateAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            LogUnknownJob(logger, jobId, context.MessageType);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (job.Status.IsTerminal())
        {
            metrics.LateResult(context.MessageType);
            LogLateResult(logger, jobId, job.Status, context.MessageType);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var finished = await steps.FinishAsync(
            jobId,
            stepKey,
            stepStatus,
            stepErrorCode,
            cancellationToken).ConfigureAwait(false);
        if (!finished)
        {
            LogStepAlreadyFinished(logger, jobId, stepKey, context.MessageType);
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        metrics.StepFinished(JobStepKeys.KindOf(stepKey), stepStatus, stepErrorCode);
        await onStepFinished(job).ConfigureAwait(false);
        if (!await jobService.SaveAsync(job, cancellationToken).ConfigureAwait(false))
        {
            LogTransitionLost(logger, job.Id, job.Status);
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Job {JobId} transition to {To} lost a race; nothing changed")]
    private static partial void LogTransitionLost(ILogger logger, Guid jobId, JobStatus to);

    [LoggerMessage(EventId = 2010, Level = LogLevel.Debug, Message = "Message {MessageId} ({MessageType}) was already processed")]
    private static partial void LogDuplicate(ILogger logger, Guid messageId, string messageType);

    [LoggerMessage(EventId = 2011, Level = LogLevel.Warning, Message = "{MessageType} for unknown job {JobId} discarded")]
    private static partial void LogUnknownJob(ILogger logger, Guid jobId, string messageType);

    [LoggerMessage(EventId = 2012, Level = LogLevel.Information, Message = "{MessageType} for job {JobId} discarded: the job is already {Status}")]
    private static partial void LogLateResult(ILogger logger, Guid jobId, JobStatus status, string messageType);

    [LoggerMessage(EventId = 2013, Level = LogLevel.Information, Message = "{MessageType} for job {JobId} step {StepKey} discarded: the step already finished")]
    private static partial void LogStepAlreadyFinished(ILogger logger, Guid jobId, string stepKey, string messageType);
}
