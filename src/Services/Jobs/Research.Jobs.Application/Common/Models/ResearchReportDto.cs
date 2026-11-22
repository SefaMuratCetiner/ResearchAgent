using Research.Jobs.Domain;

namespace Research.Jobs.Application.Common.Models;

/// <summary>Immutable projection of a finished report and its job metadata returned across the Application boundary.</summary>
public sealed record ResearchReportDto(
    Guid JobId,
    string Status,
    IReadOnlyList<string> Warnings,
    string Content,
    IReadOnlyList<ReportCitationDto> Citations,
    string Model,
    bool Truncated,
    DateTimeOffset CreatedAt)
{
    public static ResearchReportDto From(ResearchJob job, ResearchReport report)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(report);
        return new ResearchReportDto(
            job.Id,
            job.Status.ToString(),
            job.Warnings,
            report.Content,
            report.Citations.Select(static c => new ReportCitationDto(c.Number, c.Url, c.Title)).ToList(),
            report.Model,
            report.Truncated,
            report.CreatedAt);
    }
}
