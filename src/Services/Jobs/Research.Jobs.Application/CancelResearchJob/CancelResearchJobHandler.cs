using Research.Jobs.Domain;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Lifecycle;
using Research.SharedKernel.Results;
using Research.Shared.Core.Transactions;

namespace Research.Jobs.CancelResearchJob;

/// <summary>
/// Cancels an active job under its row lock, so it races safely with replies and the deadline worker (§10.1):
/// cancelling twice succeeds, a completed job answers <c>jobs.job_already_completed</c>, and a failed or expired one
/// <c>jobs.job_not_cancellable</c>.
/// </summary>
public sealed class CancelResearchJobHandler(
    ITransactionFactory transactions,
    IResearchJobRepository jobs,
    JobLifecycle lifecycle)
{
    public async Task<Result<ResearchJob>> HandleAsync(CancelResearchJobCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var transaction = await transactions.BeginAsync(cancellationToken).ConfigureAwait(false);

        var job = await jobs.GetForUpdateAsync(command.JobId, transaction, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return Result<ResearchJob>.Failure(JobsErrors.JobNotFound);
        }

        switch (job.Status)
        {
            case JobStatus.Cancelled:
                return Result<ResearchJob>.Success(job);
            case JobStatus.Completed or JobStatus.CompletedWithWarnings:
                return Result<ResearchJob>.Failure(JobsErrors.JobAlreadyCompleted);
            case JobStatus.Failed or JobStatus.Expired:
                return Result<ResearchJob>.Failure(JobsErrors.JobNotCancellable);
        }

        var cancelled = await lifecycle.CancelAsync(job, transaction, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The cancel transition failed although the job row is locked.");
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result<ResearchJob>.Success(cancelled);
    }
}
