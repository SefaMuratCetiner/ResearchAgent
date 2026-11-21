using Microsoft.Extensions.Logging;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Domain;
using Research.Jobs.Observability;
using Research.Shared.Core.Messaging;
using Research.Shared.Core.Transactions;

namespace Research.Jobs.Lifecycle;

/// <summary>
/// The frame of every reply Jobs consumes: one transaction holding the inbox record, the job's row lock and the
/// step's completion, so a reply takes effect exactly once. Duplicates, replies for unknown or terminal jobs (late
/// results, §10.1) and replies for steps that already finished are acknowledged and discarded.
/// </summary>
public sealed partial class JobReplyGate(
    IUnitOfWork unitOfWork,
    IInbox inbox,
    IResearchJobRepository jobs,
    IJobStepRepository steps,
    JobsMetrics metrics,
    ILogger<JobReplyGate> logger)
{
    /// <summary>
    /// Records the reply, finishes its step with <paramref name="stepStatus"/>, and runs <paramref name="advance"/>
    /// with the locked job, all in one transaction.
    /// </summary>
    public async Task ProcessAsync(
        Guid jobId,
        string stepKey,
        StepStatus stepStatus,
        string? stepErrorCode,
        MessageContext context,
        Func<ResearchJob, Task> advance,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(advance);

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
        await advance(job).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 2010, Level = LogLevel.Debug, Message = "Message {MessageId} ({MessageType}) was already processed")]
    private static partial void LogDuplicate(ILogger logger, Guid messageId, string messageType);

    [LoggerMessage(EventId = 2011, Level = LogLevel.Warning, Message = "{MessageType} for unknown job {JobId} discarded")]
    private static partial void LogUnknownJob(ILogger logger, Guid jobId, string messageType);

    [LoggerMessage(EventId = 2012, Level = LogLevel.Information, Message = "{MessageType} for job {JobId} discarded: the job is already {Status}")]
    private static partial void LogLateResult(ILogger logger, Guid jobId, JobStatus status, string messageType);

    [LoggerMessage(EventId = 2013, Level = LogLevel.Information, Message = "{MessageType} for job {JobId} step {StepKey} discarded: the step already finished")]
    private static partial void LogStepAlreadyFinished(ILogger logger, Guid jobId, string stepKey, string messageType);
}
