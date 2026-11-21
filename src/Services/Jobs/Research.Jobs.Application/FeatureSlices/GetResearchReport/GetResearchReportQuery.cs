using Research.Jobs.Domain;

namespace Research.Jobs.Application.GetResearchReport;

public sealed record GetResearchReportQuery(Guid JobId);

/// <summary>The report with the job's final state and warnings.</summary>
public sealed record ResearchReportView(ResearchJob Job, ResearchReport Report);
