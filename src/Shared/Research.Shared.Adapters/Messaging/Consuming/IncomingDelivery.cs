namespace Research.Shared.Adapters.Messaging.Consuming;

/// <summary>One delivery as the transport hands it to the dispatcher.</summary>
/// <param name="MessageId">The envelope's id; null when the producer did not set one (the message is then poison).</param>
/// <param name="Body">The JSON payload.</param>
/// <param name="Headers">Header values the transport could read as strings.</param>
/// <param name="Attempt">1 on the first delivery; the transport increases it when it schedules a retry.</param>
public sealed record IncomingDelivery(
    Guid? MessageId,
    ReadOnlyMemory<byte> Body,
    IReadOnlyDictionary<string, string> Headers,
    int Attempt);

/// <summary>What the transport must do with a delivery after dispatch.</summary>
public enum DispatchOutcome
{
    /// <summary>Done (successfully, or as a reported business failure): acknowledge.</summary>
    Ack,

    /// <summary>Failed transiently: republish to the delay queue <see cref="DispatchResult.RetryDelayIndex"/>, then acknowledge.</summary>
    Retry,

    /// <summary>Poison or exhausted: move to the dead-letter queue.</summary>
    DeadLetter,

    /// <summary>Interrupted by shutdown: return to the queue untouched for another replica.</summary>
    Requeue,
}

/// <summary>The dispatcher's verdict on one delivery.</summary>
public readonly record struct DispatchResult(DispatchOutcome Outcome, int RetryDelayIndex = 0)
{
    public static DispatchResult Ack { get; } = new(DispatchOutcome.Ack);

    public static DispatchResult DeadLetter { get; } = new(DispatchOutcome.DeadLetter);

    public static DispatchResult Requeue { get; } = new(DispatchOutcome.Requeue);

    public static DispatchResult Retry(int delayIndex) => new(DispatchOutcome.Retry, delayIndex);
}
