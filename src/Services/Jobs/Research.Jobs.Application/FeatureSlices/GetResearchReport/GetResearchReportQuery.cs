using Research.Jobs.Application.Common.Models;
using Research.Shared.Bus.Mediation;
using Research.Shared.Kernel.Results;

namespace Research.Jobs.Application.GetResearchReport;

public sealed record GetResearchReportQuery(Guid JobId) : IQuery<Result<ResearchReportDto>>, IJobScopedRequest;
