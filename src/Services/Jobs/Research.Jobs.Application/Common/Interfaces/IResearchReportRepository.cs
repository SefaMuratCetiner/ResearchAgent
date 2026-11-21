using Research.Jobs.Domain;

namespace Research.Jobs.Application.Common.Interfaces;

/// <summary>Persistence of <see cref="ResearchReport"/>, one per job.</summary>
public interface IResearchReportRepository
{
    Task InsertAsync(ResearchReport report, CancellationToken cancellationToken);

    Task<ResearchReport?> GetAsync(Guid jobId, CancellationToken cancellationToken);
}
