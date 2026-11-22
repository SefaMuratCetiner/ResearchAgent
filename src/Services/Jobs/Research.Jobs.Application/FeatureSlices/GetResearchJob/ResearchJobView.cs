using Research.Jobs.Application.Common.Models;

namespace Research.Jobs.Application.GetResearchJob;

/// <summary>A job DTO with its step progress.</summary>
public sealed record ResearchJobView(ResearchJobDto Job, IReadOnlyList<StepProgressDto> Steps);
