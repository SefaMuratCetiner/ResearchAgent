using Research.Shared.Core.Errors;
using Research.SharedKernel.Results;

namespace Research.Shared.Core.Messaging;

/// <summary>Errors raised by the shared consumer pipeline. They travel as <c>StepFailed.ErrorCode</c>.</summary>
public static class MessagingErrors
{
    public static readonly Error StepExhausted = new(
        "research.step_exhausted",
        ErrorKind.Unexpected,
        "The step failed on every attempt.");

    public static readonly Error PoisonMessage = new(
        "research.poison_message",
        ErrorKind.Invalid,
        "The message could not be read.");
}

/// <summary>Adds <see cref="MessagingErrors"/> to the error catalog.</summary>
public sealed class MessagingErrorCatalog : IErrorCatalogContributor
{
    public string Prefix => "research.";

    public IEnumerable<Error> GetErrors() => [MessagingErrors.StepExhausted, MessagingErrors.PoisonMessage];
}
