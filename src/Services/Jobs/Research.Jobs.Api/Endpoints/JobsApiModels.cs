using Research.Jobs.Application.GetResearchJob;
using Research.Jobs.Domain;

namespace Research.Jobs.Api.Endpoints;

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
    public static ResearchJobResponse From(ResearchJob job, IReadOnlyList<StepProgress>? steps = null)
    {
        ArgumentNullException.ThrowIfNull(job);
        return new ResearchJobResponse(
            job.Id,
            job.Question,
            job.Status.ToString(),
            job.Plan,
            job.ErrorCode,
            job.Warnings,
            job.TokensUsed,
            job.TokenBudget,
            job.CreatedAt,
            job.DeadlineAt,
            job.CompletedAt,
            (steps ?? []).Select(static s => new StepProgressResponse(s.Kind.ToString(), s.Running, s.Succeeded, s.Failed)).ToList(),
            ResearchJobLinks.For(job.Id));
    }

    public static ResearchJobResponse From(ResearchJobView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return From(view.Job, view.Steps);
    }
}

/// <summary>Step progress of one kind.</summary>
public sealed record StepProgressResponse(string Kind, int Running, int Succeeded, int Failed);

/// <summary>Where to go next.</summary>
public sealed record ResearchJobLinks(string Self, string Report, string Events, string Cancel)
{
    public static ResearchJobLinks For(Guid id)
    {
        var self = "/research/" + id.ToString("D");
        return new ResearchJobLinks(self, self + "/report", self + "/events", self + "/cancel");
    }
}
