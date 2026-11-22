namespace Research.Shared.Adapters.Messaging.Consuming;

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
