using Research.Jobs.Application.Common.Models;
using Research.Jobs.Application.GetResearchJob;

namespace Research.Jobs.Api.Common.Models;

/// <summary>A research job as the API shows it (shared across create, get, cancel, and SSE endpoints).</summary>
public sealed record ResearchJobResponse(
    Guid Id,
    string Question,
    string Status,
    IReadOnlyList<string> Plan,
    string? ErrorCode,
    IReadOnlyList<string> Warnings,
    long TokensUsed,
    long TokenBudget,
    DateTimeOffset CreatedAt,
    DateTimeOffset DeadlineAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<StepProgressResponse> Steps,
    ResearchJobLinks Links)
{
    public static ResearchJobResponse From(ResearchJobDto job, IReadOnlyList<StepProgressDto>? steps = null)
    {
        ArgumentNullException.ThrowIfNull(job);
        return new ResearchJobResponse(
            job.Id,
            job.Question,
            job.Status,
            job.Plan,
            job.ErrorCode,
            job.Warnings,
            job.TokensUsed,
            job.TokenBudget,
            job.CreatedAt,
            job.DeadlineAt,
            job.CompletedAt,
            (steps ?? []).Select(static s => new StepProgressResponse(s.Kind, s.Running, s.Succeeded, s.Failed)).ToList(),
            ResearchJobLinks.For(job.Id));
    }

    public static ResearchJobResponse From(ResearchJobView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return From(view.Job, view.Steps);
    }
}
