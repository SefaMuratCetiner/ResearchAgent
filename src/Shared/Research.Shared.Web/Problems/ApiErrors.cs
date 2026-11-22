using Research.Shared.Kernel.Results;

namespace Research.Shared.Web.Problems;

/// <summary>The shared HTTP errors every API can return.</summary>
public static class ApiErrors
{
    public static readonly Error Unexpected = new(
        "research.unexpected",
        ErrorKind.Unexpected,
        "An unexpected error occurred.");

    public static readonly Error ValidationFailed = new(
        "research.validation_failed",
        ErrorKind.Invalid,
        "The request is not valid.");

    public static readonly ErrorSet All = new("research.", [Unexpected, ValidationFailed]);
}
