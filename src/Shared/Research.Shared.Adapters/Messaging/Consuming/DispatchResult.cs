using System.Runtime.InteropServices;

namespace Research.Shared.Adapters.Messaging.Consuming;

/// <summary>The dispatcher's verdict on one delivery.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DispatchResult(DispatchOutcome Outcome, int RetryDelayIndex = 0)
{
    public static DispatchResult Ack { get; } = new(DispatchOutcome.Ack);

    public static DispatchResult DeadLetter { get; } = new(DispatchOutcome.DeadLetter);

    public static DispatchResult Requeue { get; } = new(DispatchOutcome.Requeue);

    public static DispatchResult Retry(int delayIndex) => new(DispatchOutcome.Retry, delayIndex);
}
