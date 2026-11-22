using Microsoft.Extensions.Options;
using Research.Jobs.Domain;
using Research.Shared.Core.Mediation;
using Research.Shared.Core.Security;
using Research.SharedKernel.Results;

namespace Research.Jobs.Application.CreateResearchJob;

/// <summary>Start researching <see cref="Question"/>. A repeated <see cref="IdempotencyKey"/> returns the first job.</summary>
public sealed record CreateResearchJobCommand(string Question, string? IdempotencyKey)
    : ICommand<Result<CreateResearchJobResult>>;

/// <summary>The job, and whether this call created it (false: an earlier call with the same key did).</summary>
public sealed record CreateResearchJobResult(ResearchJob Job, bool Created);

/// <summary>Validates the cleaned question length before <see cref="CreateResearchJobHandler"/> runs.</summary>
public sealed class CreateResearchJobCommandValidator(IOptionsSnapshot<JobsOptions> options)
    : IRequestValidator<CreateResearchJobCommand>
{
    public ValueTask<Error?> ValidateAsync(CreateResearchJobCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = options.Value;
        var question = UntrustedText.Clean(request.Question).Trim();
        return ValueTask.FromResult(
            question.Length < settings.QuestionMinLength || question.Length > settings.QuestionMaxLength
                ? JobsErrors.QuestionInvalid
                : null);
    }
}
