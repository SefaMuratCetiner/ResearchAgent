using Research.Jobs.Application.Common.Models;

namespace Research.Jobs.Api.GetResearchReport;

/// <summary>A finished report.</summary>
public sealed record ResearchReportResponse(
    Guid JobId,
    string Status,
    IReadOnlyList<string> Warnings,
    string Content,
    IReadOnlyList<CitationResponse> Citations,
    string Model,
    bool Truncated,
    DateTimeOffset CreatedAt)
{
    public static ResearchReportResponse From(ResearchReportDto report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return new ResearchReportResponse(
            report.JobId,
            report.Status,
            report.Warnings,
            report.Content,
            report.Citations.Select(static c => new CitationResponse(c.Number, c.Url, c.Title)).ToList(),
            report.Model,
            report.Truncated,
            report.CreatedAt);
    }
}
