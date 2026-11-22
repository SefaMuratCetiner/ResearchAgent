using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Application.Common.Models;
using Research.Jobs.Domain;
using Research.Shared.Core.Mediation;
using Research.SharedKernel.Results;

namespace Research.Jobs.Application.CancelResearchJob;

/// <summary>
/// Cancels an active job under its row lock, delegating all cancellation invariants to <see cref="ResearchJob.Cancel"/>.
/// Runs inside a single <see cref="Research.Shared.Core.Transactions.IUnitOfWork"/> managed by <c>TransactionBehavior</c>.
/// </summary>
public sealed class CancelResearchJobHandler(
    IResearchJobRepository jobs,
    TimeProvider timeProvider) : ICommandHandler<CancelResearchJobCommand, Result<ResearchJobDto>>
{
    public async Task<Result<ResearchJobDto>> HandleAsync(CancelResearchJobCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

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

        if (!wasCancelled && !await jobs.UpdateAsync(job, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The cancel transition failed although the job row is locked.");
        }

        return Result<ResearchJobDto>.Success(ResearchJobDto.From(job));
    }
}
