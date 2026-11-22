namespace Research.Shared.Messaging;

/// <summary>
/// How the outbox retries a message whose publish failed. The wait before the next attempt starts at
/// <paramref name="FirstDelay"/> and doubles up to <paramref name="MaxDelay"/>; after <paramref name="MaxAttempts"/>
/// failed attempts the message is parked: kept, but never tried again until someone requeues it.
/// </summary>
public sealed record OutboxRetryPolicy(int MaxAttempts, TimeSpan FirstDelay, TimeSpan MaxDelay)
{
    /// <param name="failedAttempts">Failed attempts so far, counting the one that just failed (1 or more).</param>
    public TimeSpan DelayAfter(int failedAttempts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failedAttempts, 1);
        var exponent = Math.Min(failedAttempts - 1, 30);
        var delay = FirstDelay * Math.Pow(2, exponent);
        return delay < MaxDelay ? delay : MaxDelay;
    }

    /// <param name="failedAttempts">Failed attempts so far, counting the one that just failed (1 or more).</param>
    public bool ShouldPark(int failedAttempts) => failedAttempts >= MaxAttempts;
}
