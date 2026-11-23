using Research.Jobs.Application.Common.Models;
using Research.Shared.Bus.Mediation;
using Research.Shared.Kernel.Results;

namespace Research.Jobs.Application.CancelResearchJob;

/// <summary>Stop a job. Work already in flight may finish and be billed; its result is discarded.</summary>
public sealed record CancelResearchJobCommand(Guid JobId)
    : ICommand<Result<ResearchJobDto>>, IJobScopedRequest;
