using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Domain;
using Research.Jobs.Lifecycle;
using Research.Shared.Core.Transactions;
using Research.SharedKernel.Results;

namespace Research.Jobs.CancelResearchJob;

/// <summary>
/// Cancels an active job under its row lock, delegating all cancellation invariants to <see cref="ResearchJob.Cancel"/>.
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

        var result = await lifecycle.CancelAsync(job, transaction, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result;
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}
