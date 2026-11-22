namespace Research.Shared.Bus.Messaging;

/// <summary>The dispatcher's view of the outbox table.</summary>
public interface IOutboxStore
{
    /// <summary>
    /// Locks up to <paramref name="batchSize"/> unpublished messages that are due (skipping rows other replicas hold),
    /// calls <paramref name="publish"/> for each in order, and marks the published ones. A message whose publish throws
    /// stays pending with its attempt count increased and is not due again until the <paramref name="retry"/> delay
    /// has passed, so it never holds back the messages behind it; after <see cref="OutboxRetryPolicy.MaxAttempts"/> it
    /// is parked. The rest of the batch waits for the next run (the broker is probably unreachable). Order is best
    /// effort only; consumers never rely on it.
    /// </summary>
    /// <returns>How many messages were published.</returns>
    Task<int> PublishPendingAsync(
        Func<OutgoingEnvelope, CancellationToken, Task> publish,
        int batchSize,
        OutboxRetryPolicy retry,
        CancellationToken cancellationToken);

    /// <summary>Waits until a new message is signalled or <paramref name="timeout"/> passes, whichever comes first.</summary>
    Task WaitForSignalAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
