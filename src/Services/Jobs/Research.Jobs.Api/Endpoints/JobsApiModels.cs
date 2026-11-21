using Research.Jobs.Domain;
using Research.Jobs.Application.GetResearchJob;
using Research.Jobs.Application.GetResearchReport;

namespace Research.Jobs.Api.Endpoints;

/// <summary>Body of <c>POST /research</c>.</summary>
public sealed record CreateResearchJobRequest(string? Question);

/// <summary>A research job as the API shows it.</summary>
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
    public static ResearchReportResponse From(ResearchReportView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new ResearchReportResponse(
            view.Job.Id,
            view.Job.Status.ToString(),
            view.Job.Warnings,
            view.Report.Content,
            view.Report.Citations.Select(static c => new CitationResponse(c.Number, c.Url, c.Title)).ToList(),
            view.Report.Model,
            view.Report.Truncated,
            view.Report.CreatedAt);
    }
}

/// <summary>One numbered source; <c>[n]</c> in the report text refers to <see cref="Number"/>.</summary>
public sealed record CitationResponse(int Number, string Url, string Title);
