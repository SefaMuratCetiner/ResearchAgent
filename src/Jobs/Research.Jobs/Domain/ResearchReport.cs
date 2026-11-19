using Research.SharedKernel.Entities;

namespace Research.Jobs.Domain;

/// <summary>The finished report of a job. <see cref="Entity.Id"/> equals the job id (one report per job).</summary>
public sealed record ResearchReport : AuditableEntity
{
    public required string Content { get; init; }

    public required IReadOnlyList<ReportSource> Citations { get; init; }

    public required string Model { get; init; }

    public bool Truncated { get; init; }
}

/// <summary>One numbered source of a report.</summary>
public sealed record ReportSource(int Number, string Url, string Title);
