using Research.Shared.Kernel.Results;

namespace Research.Shared.Core.Mediation;

/// <summary>Strategy for validating a request before its handler runs; returns <c>null</c> when valid or an <see cref="Error"/> when invalid.</summary>
public interface IRequestValidator<in TRequest>
{
    ValueTask<Error?> ValidateAsync(TRequest request, CancellationToken cancellationToken);
}
