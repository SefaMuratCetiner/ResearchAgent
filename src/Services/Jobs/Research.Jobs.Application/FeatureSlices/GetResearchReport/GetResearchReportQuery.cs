using Research.Jobs.Application.Common.Models;
using Research.Shared.Core.Mediation;
using Research.SharedKernel.Results;

namespace Research.Jobs.Application.GetResearchReport;

public sealed record GetResearchReportQuery(Guid JobId) : IQuery<Result<ResearchReportDto>>, IJobScopedRequest;
