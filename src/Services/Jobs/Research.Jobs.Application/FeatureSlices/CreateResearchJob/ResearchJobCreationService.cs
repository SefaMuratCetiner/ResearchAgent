using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Application.Common.Models;
using Research.Jobs.Application.Common.Services;
using Research.Jobs.Domain;
using Research.Shared.Core.Security;
using Research.Shared.Bus.Transactions;
using Research.Shared.Kernel.Results;

namespace Research.Jobs.Application.CreateResearchJob;

/// <summary>
/// Creates the job in <see cref="JobStatus.Planning"/> with both clocks set. <see cref="ResearchJobService"/> publishes
/// <c>JobCreated</c>, whose handler starts the plan step (<c>PlanRequested</c>) inside the same transaction (§16.1).
/// </summary>
public sealed partial class ResearchJobCreationService(
    IUnitOfWork unitOfWork,
    IResearchJobRepository jobs,
    ResearchJobService jobService,
    IOptionsSnapshot<JobsOptions> options,
    TimeProvider timeProvider,
    ILogger<ResearchJobCreationService> logger)
{
    public async Task<Result<CreateResearchJobResult>> CreateAsync(CreateResearchJobCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var settings = options.Value;
        // Invisible characters are removed before anything sees the question (OWASP ASI01).
        // Length bounds are enforced upstream by CreateResearchJobCommandValidator via ValidationBehavior.
        var question = UntrustedText.Clean(command.Question).Trim();

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
        var job = ResearchJob.Create(
            Guid.CreateVersion7(now),
            question,
            command.IdempotencyKey,
            settings.MaxDuration,
            settings.SynthesisReserve,
            settings.TokenBudget,
            now);

        await using (await unitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await jobService.CreateAsync(job, cancellationToken).ConfigureAwait(false))
            {
                // Another request with the same key won the insert race; answer with its job.
                var winner = await jobs.FindByIdempotencyKeyAsync(command.IdempotencyKey!, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("The idempotency key conflicted but no job holds it.");
                return Replay(winner, question);
            }

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        LogJobCreated(logger, job.Id, job.DeadlineAt);
        return Result<CreateResearchJobResult>.Success(new CreateResearchJobResult(ResearchJobDto.From(job), Created: true));
    }

    private static Result<CreateResearchJobResult> Replay(ResearchJob earlier, string question) =>
        string.Equals(earlier.Question, question, StringComparison.Ordinal)
            ? Result<CreateResearchJobResult>.Success(new CreateResearchJobResult(ResearchJobDto.From(earlier), Created: false))
            : Result<CreateResearchJobResult>.Failure(JobsErrors.IdempotencyConflict);

    [LoggerMessage(EventId = 2020, Level = LogLevel.Information, Message = "Job {JobId} created; deadline {DeadlineAt}")]
    private static partial void LogJobCreated(ILogger logger, Guid jobId, DateTimeOffset deadlineAt);

    [LoggerMessage(EventId = 2021, Level = LogLevel.Warning, Message = "Job refused: {MaxActiveJobs} jobs are already active")]
    private static partial void LogCapacityReached(ILogger logger, int maxActiveJobs);
}
