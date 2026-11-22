using Research.Jobs.Domain;

namespace Research.Jobs.Application.Common.Models;

/// <summary>Immutable read projection of a <see cref="ResearchJob"/> returned across the Application boundary.</summary>
public sealed record ResearchJobDto(
    Guid Id,
    string Question,
    string Status,
    bool IsTerminal,
    int Version,
    IReadOnlyList<string> Plan,
    string? ErrorCode,
    IReadOnlyList<string> Warnings,
    long TokensUsed,
    long TokenBudget,
    DateTimeOffset CreatedAt,
    DateTimeOffset DeadlineAt,
    DateTimeOffset? CompletedAt)
{
    public static ResearchJobDto From(ResearchJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return new ResearchJobDto(
            job.Id,
            job.Question,
            job.Status.ToString(),
            job.Status.IsTerminal(),
            job.Version,
            job.Plan,
            job.ErrorCode,
            job.Warnings,
            job.TokensUsed,
            job.TokenBudget,
            job.CreatedAt,
            job.DeadlineAt,
            job.CompletedAt);
    }
}

/// <summary>How many steps of one kind are in each state.</summary>
public sealed record StepProgressDto(string Kind, int Running, int Succeeded, int Failed);

/// <summary>One numbered source cited by a finished report.</summary>
public sealed record ReportCitationDto(int Number, string Url, string Title);

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
