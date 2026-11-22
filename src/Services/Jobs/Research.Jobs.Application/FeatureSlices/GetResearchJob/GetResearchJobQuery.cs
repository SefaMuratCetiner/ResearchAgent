using Research.Jobs.Application.Common.Models;
using Research.Shared.Core.Mediation;
using Research.SharedKernel.Results;

namespace Research.Jobs.Application.GetResearchJob;

public sealed record GetResearchJobQuery(Guid JobId) : IQuery<Result<ResearchJobView>>;

/// <summary>A job DTO with its step progress.</summary>
public sealed record ResearchJobView(ResearchJobDto Job, IReadOnlyList<StepProgressDto> Steps);
