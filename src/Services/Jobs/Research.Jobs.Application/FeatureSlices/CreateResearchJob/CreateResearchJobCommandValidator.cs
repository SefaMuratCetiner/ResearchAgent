using Microsoft.Extensions.Options;
using Research.Jobs.Domain;
using Research.Shared.Core.Mediation;
using Research.Shared.Kernel.Results;
using Research.Shared.Kernel.Security;

namespace Research.Jobs.Application.CreateResearchJob;

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
