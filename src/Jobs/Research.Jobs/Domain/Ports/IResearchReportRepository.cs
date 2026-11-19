using Research.SharedKernel.Transactions;

namespace Research.Jobs.Domain.Ports;

/// <summary>Persistence of <see cref="ResearchReport"/>, one per job.</summary>
public interface IResearchReportRepository
{
    Task InsertAsync(ResearchReport report, ITransactionScope transaction, CancellationToken cancellationToken);

    Task<ResearchReport?> GetAsync(Guid jobId, CancellationToken cancellationToken);
}
