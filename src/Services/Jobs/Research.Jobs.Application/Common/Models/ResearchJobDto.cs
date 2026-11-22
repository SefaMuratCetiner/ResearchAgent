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
