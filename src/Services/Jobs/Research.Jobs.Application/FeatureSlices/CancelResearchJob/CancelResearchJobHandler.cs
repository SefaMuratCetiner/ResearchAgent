using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Application.Lifecycle;
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
    JobLifecycle lifecycle) : ICommandHandler<CancelResearchJobCommand, Result<ResearchJob>>
{
    public async Task<Result<ResearchJob>> HandleAsync(CancelResearchJobCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var job = await jobs.GetForUpdateAsync(command.JobId, cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return Result<ResearchJob>.Failure(JobsErrors.JobNotFound);
        }

        return await lifecycle.CancelAsync(job, cancellationToken).ConfigureAwait(false);
    }
}
