using Research.Shared.Bus.Mediation;
using Research.Shared.Kernel.Results;

namespace Research.Jobs.Application.CreateResearchJob;

/// <summary>Creates a research job through <see cref="ResearchJobCreationService"/>.</summary>
public sealed class CreateResearchJobHandler(ResearchJobCreationService creation)
    : ICommandHandler<CreateResearchJobCommand, Result<CreateResearchJobResult>>
{
    public Task<Result<CreateResearchJobResult>> HandleAsync(CreateResearchJobCommand command, CancellationToken cancellationToken) =>
        creation.CreateAsync(command, cancellationToken);
}
