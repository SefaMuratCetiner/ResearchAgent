using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Application.Lifecycle;
using Research.Jobs.Application.Observability;
using Research.Jobs.Domain;
using Research.Shared.Core.Mediation;
using Research.Shared.Messaging;
using Research.Shared.Core.Transactions;

namespace Research.Jobs.Application.RecoverStuckJobs;

/// <summary>Re-emit or fail expired steps on active jobs; returns how many steps were handled.</summary>
public sealed record RecoverStuckJobsCommand : ICommand<int>;

/// <summary>
/// Scheduler-agent-supervisor: a running step whose lease expired without an answer (a lost message, a broker that
/// lost its queues, a crashed worker) is re-emitted from its stored request, up to
/// <see cref="JobsOptions.MaxStepAttempts"/>; after that it fails with <c>research.step_exhausted</c> like any other
/// failed step. Jobs are locked one at a time with <c>SKIP LOCKED</c>, so every replica can run this safely.
/// </summary>
public sealed partial class RecoverStuckJobsHandler(
    IUnitOfWork unitOfWork,
    IResearchJobRepository jobs,
    IJobStepRepository steps,
    JobLifecycle lifecycle,
    IOutbox outbox,
    IEnvelopeFactory envelopes,
    JobsMetrics metrics,
    IOptionsSnapshot<JobsOptions> options,
    TimeProvider timeProvider,
    ILogger<RecoverStuckJobsHandler> logger) : ICommandHandler<RecoverStuckJobsCommand, int>
{
    public Task<int> HandleAsync(RecoverStuckJobsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return HandleAsync(cancellationToken);
    }

    /// <returns>How many steps were re-emitted or failed.</returns>
    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var jobIds = await jobs.ListWithExpiredStepsAsync(now, settings.WorkerBatchSize, cancellationToken).ConfigureAwait(false);

        var handled = 0;
        foreach (var jobId in jobIds)
        {
            handled += await RecoverJobAsync(jobId, now, settings, cancellationToken).ConfigureAwait(false);
        }

        return handled;
    }

    private async Task<int> RecoverJobAsync(Guid jobId, DateTimeOffset now, JobsOptions settings, CancellationToken cancellationToken)
    {
        await using var _ = await unitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);
        var job = await jobs.TryLockAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (job is null || job.Status.IsTerminal())
        {
            return 0;
        }

        var expired = await steps.ListExpiredAsync(jobId, now, cancellationToken).ConfigureAwait(false);
        var gatheringStepFailed = false;
        var handled = 0;

        foreach (var step in expired)
        {
            handled++;
            if (step.Attempts < settings.MaxStepAttempts)
            {
                var envelope = envelopes.Recreate(step.RequestType, step.RequestPayload, jobId, step.StepKey);
                await outbox.EnqueueAsync(envelope, cancellationToken).ConfigureAwait(false);
                await steps.RenewLeaseAsync(step.Id, step.Attempts + 1, now + settings.StepLease, cancellationToken)
                    .ConfigureAwait(false);
                metrics.StepReemitted(step.Kind);
                LogReemitted(logger, jobId, step.StepKey, step.Attempts + 1);
                continue;
            }

            var code = MessagingErrors.StepExhausted.Code;
            await steps.FinishAsync(jobId, step.StepKey, StepStatus.Failed, code, cancellationToken)
                .ConfigureAwait(false);
            metrics.StepFinished(step.Kind, StepStatus.Failed, code);
            LogExhausted(logger, jobId, step.StepKey, step.Attempts);

            if (step.Kind is StepKind.Plan or StepKind.Synthesize)
            {
                await lifecycle.FailAsync(job, code, cancellationToken).ConfigureAwait(false);
                await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
                return handled;
            }

            gatheringStepFailed = true;
        }

        if (gatheringStepFailed)
        {
            await lifecycle.EvaluateGatheringAsync(job, forcedReason: null, cancellationToken).ConfigureAwait(false);
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return handled;
    }

    [LoggerMessage(EventId = 2030, Level = LogLevel.Warning, Message = "Job {JobId} step {StepKey} lease expired; re-emitted (attempt {Attempt})")]
    private static partial void LogReemitted(ILogger logger, Guid jobId, string stepKey, int attempt);

    [LoggerMessage(EventId = 2031, Level = LogLevel.Warning, Message = "Job {JobId} step {StepKey} got no answer after {Attempts} attempts; failed")]
    private static partial void LogExhausted(ILogger logger, Guid jobId, string stepKey, int attempts);
}
