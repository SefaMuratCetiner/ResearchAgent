using Research.Shared.Kernel.Results;

namespace Research.Shared.Messaging;

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

    public static readonly ErrorSet All = new("research.", [StepExhausted, PoisonMessage]);
}
