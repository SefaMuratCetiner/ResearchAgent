using Research.Jobs.Domain;
using Research.Shared.Core.Transactions;

namespace Research.Jobs.Application.Common.Interfaces;

/// <summary>Persistence of <see cref="ResearchReport"/>, one per job.</summary>
public interface IResearchReportRepository
{
    Task InsertAsync(ResearchReport report, ITransactionScope transaction, CancellationToken cancellationToken);

    Task<ResearchReport?> GetAsync(Guid jobId, CancellationToken cancellationToken);
}
