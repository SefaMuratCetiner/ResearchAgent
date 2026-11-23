using Research.Jobs.Application.Common.Models;
using Research.Shared.Bus.Mediation;
using Research.Shared.Kernel.Results;

namespace Research.Jobs.Application.CancelResearchJob;

/// <summary>Cancels a research job through <see cref="ResearchJobCancellationService"/>.</summary>
public sealed class CancelResearchJobHandler(ResearchJobCancellationService cancellation)
    : ICommandHandler<CancelResearchJobCommand, Result<ResearchJobDto>>
{
    public Task<Result<ResearchJobDto>> HandleAsync(CancelResearchJobCommand command, CancellationToken cancellationToken) =>
        cancellation.CancelAsync(command, cancellationToken);
}
