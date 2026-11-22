using Research.Jobs.Domain;
using Research.Shared.Core.Mediation;
using Research.SharedKernel.Results;

namespace Research.Jobs.Application.GetResearchReport;

public sealed record GetResearchReportQuery(Guid JobId) : IQuery<Result<ResearchReportView>>;

/// <summary>The report with the job's final state and warnings.</summary>
public sealed record ResearchReportView(ResearchJob Job, ResearchReport Report);
