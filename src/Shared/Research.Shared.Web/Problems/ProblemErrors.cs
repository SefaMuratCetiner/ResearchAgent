using Research.Shared.Core.Errors;
using Research.SharedKernel.Results;

namespace Research.Shared.Web.Problems;

/// <summary>The shared HTTP errors every API can return.</summary>
public static class ProblemErrors
{
    public static readonly Error Unexpected = new(
        "research.unexpected",
        ErrorKind.Unexpected,
        "An unexpected error occurred.");

    public static readonly Error ValidationFailed = new(
        "research.validation_failed",
        ErrorKind.Invalid,
        "The request is not valid.");
}

/// <summary>Adds <see cref="ProblemErrors"/> to the error catalog.</summary>
public sealed class ProblemErrorCatalog : IErrorCatalogContributor
{
    public string Prefix => "research.";

    public IEnumerable<Error> GetErrors() => [ProblemErrors.Unexpected, ProblemErrors.ValidationFailed];
}
