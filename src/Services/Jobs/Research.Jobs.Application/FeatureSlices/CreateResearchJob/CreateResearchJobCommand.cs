using Research.Shared.Core.Mediation;
using Research.Shared.Kernel.Results;

namespace Research.Jobs.Application.CreateResearchJob;

/// <summary>Start researching <see cref="Question"/>. A repeated <see cref="IdempotencyKey"/> returns the first job.</summary>
public sealed record CreateResearchJobCommand(string Question, string? IdempotencyKey)
    : ICommand<Result<CreateResearchJobResult>>;
