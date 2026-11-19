using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Contracts.Jobs;
using Research.Contracts.Planning;
using Research.Jobs.Domain;
using Research.Jobs.Domain.Ports;
using Research.Jobs.Lifecycle;
using Research.Jobs.Observability;
using Research.Messaging;
using Research.SharedKernel.Actors;
using Research.SharedKernel.Messaging;
using Research.SharedKernel.Results;
using Research.SharedKernel.Security;
using Research.SharedKernel.Transactions;

namespace Research.Jobs.CreateResearchJob;

/// <summary>
/// Creates the job in <see cref="JobStatus.Planning"/> with both clocks set, and in the same transaction starts the
/// plan step (<c>PlanRequested</c>) and announces <c>JobCreated</c>. When this returns, nothing can be lost (§16.1).
/// </summary>
public sealed partial class CreateResearchJobHandler(
    ITransactionFactory transactions,
    IResearchJobRepository jobs,
    StepEmitter emitter,
    IOutbox outbox,
    EnvelopeFactory envelopes,
    JobsMetrics metrics,
    IOptionsSnapshot<JobsOptions> options,
    TimeProvider timeProvider,
    ICurrentActor actor,
    ILogger<CreateResearchJobHandler> logger)
{
    public async Task<Result<CreateResearchJobResult>> HandleAsync(CreateResearchJobCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var settings = options.Value;
        // Invisible characters are removed before anything sees the question (OWASP ASI01).
        var question = UntrustedText.Clean(command.Question).Trim();
        if (question.Length < settings.QuestionMinLength || question.Length > settings.QuestionMaxLength)
        {
            return Result<CreateResearchJobResult>.Failure(JobsErrors.QuestionInvalid);
        }

        if (command.IdempotencyKey is { } key
            && await jobs.FindByIdempotencyKeyAsync(key, cancellationToken).ConfigureAwait(false) is { } earlier)
        {
            return Replay(earlier, question);
        }

        if (settings.MaxActiveJobs > 0
            && await jobs.CountActiveAsync(settings.MaxActiveJobs, cancellationToken).ConfigureAwait(false) >= settings.MaxActiveJobs)
        {
            LogCapacityReached(logger, settings.MaxActiveJobs);
            return Result<CreateResearchJobResult>.Failure(JobsErrors.CapacityReached);
        }

        var now = timeProvider.GetUtcNow();
        var job = new ResearchJob
        {
            Id = Guid.CreateVersion7(now),
            Question = question,
            Status = JobStatus.Planning,
            IdempotencyKey = command.IdempotencyKey,
            SynthesisCutoffAt = now + settings.MaxDuration - settings.SynthesisReserve,
            DeadlineAt = now + settings.MaxDuration,
            TokenBudget = settings.TokenBudget,
            CreatedAt = now,
            CreatedBy = actor.Id,
        };

        await using (var transaction = await transactions.BeginAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await jobs.InsertAsync(job, transaction, cancellationToken).ConfigureAwait(false))
            {
                // Another request with the same key won the insert race; answer with its job.
                var winner = await jobs.FindByIdempotencyKeyAsync(command.IdempotencyKey!, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("The idempotency key conflicted but no job holds it.");
                return Replay(winner, question);
            }

            await emitter.EmitAsync(
                new PlanRequested(job.Id, JobStepKeys.Plan, question, settings.MaxSubQuestions),
                transaction,
                cancellationToken).ConfigureAwait(false);
            await outbox.EnqueueAsync(envelopes.Create(new JobCreated(job.Id, "job", question)), transaction, cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        metrics.Created();
        LogJobCreated(logger, job.Id, job.DeadlineAt);
        return Result<CreateResearchJobResult>.Success(new CreateResearchJobResult(job, Created: true));
    }

    private static Result<CreateResearchJobResult> Replay(ResearchJob earlier, string question) =>
        string.Equals(earlier.Question, question, StringComparison.Ordinal)
            ? Result<CreateResearchJobResult>.Success(new CreateResearchJobResult(earlier, Created: false))
            : Result<CreateResearchJobResult>.Failure(JobsErrors.IdempotencyConflict);

    [LoggerMessage(EventId = 2020, Level = LogLevel.Information, Message = "Job {JobId} created; deadline {DeadlineAt}")]
    private static partial void LogJobCreated(ILogger logger, Guid jobId, DateTimeOffset deadlineAt);

    [LoggerMessage(EventId = 2021, Level = LogLevel.Warning, Message = "Job refused: {MaxActiveJobs} jobs are already active")]
    private static partial void LogCapacityReached(ILogger logger, int maxActiveJobs);
}
