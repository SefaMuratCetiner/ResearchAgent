using Research.Jobs.Domain;
using Research.Shared.Core.Mediation;
using Research.SharedKernel.Results;

namespace Research.Jobs.Application.GetResearchJob;

public sealed record GetResearchJobQuery(Guid JobId) : IQuery<Result<ResearchJobView>>;

/// <summary>A job with its step progress.</summary>
public sealed record ResearchJobView(ResearchJob Job, IReadOnlyList<StepProgress> Steps);

/// <summary>How many steps of one kind are in each state.</summary>
public sealed record StepProgress(StepKind Kind, int Running, int Succeeded, int Failed);
