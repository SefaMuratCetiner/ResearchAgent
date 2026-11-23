using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Application.Common.Models;
using Research.Jobs.Application.Common.Services;
using Research.Jobs.Domain;
using Research.Shared.Bus.Transactions;
using Research.Shared.Kernel.Results;

namespace Research.Jobs.Application.CancelResearchJob;

/// <summary>
/// Cancels an active job under its row lock, delegating all cancellation invariants to <see cref="ResearchJob.Cancel"/>.
/// </summary>
public sealed class ResearchJobCancellationService(
    IUnitOfWork unitOfWork,
    IResearchJobRepository jobs,
    ResearchJobService jobService,
    TimeProvider timeProvider)
{
    public async Task<Result<ResearchJobDto>> CancelAsync(CancelResearchJobCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var _ = await unitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);

        var job = await jobs.GetForUpdateAsync(command.JobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return Result<ResearchJobDto>.Failure(JobsErrors.JobNotFound);
        }

        var wasCancelled = job.Status == JobStatus.Cancelled;
        var decision = job.Cancel(timeProvider.GetUtcNow());
        if (!decision.IsSuccess)
        {
            return Result<ResearchJobDto>.Failure(decision.Error);
        }

        if (!wasCancelled)
        {
            if (!await jobService.SaveAsync(job, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("The cancel transition failed although the job row is locked.");
            }

            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result<ResearchJobDto>.Success(ResearchJobDto.From(job));
    }
}
