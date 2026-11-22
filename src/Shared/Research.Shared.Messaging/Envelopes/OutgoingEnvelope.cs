namespace Research.Shared.Messaging;

/// <summary>
/// A serialized integration message on its way out: what the outbox stores and the publisher sends.
/// </summary>
/// <param name="MessageId">Unique per message; consumers use it for idempotency (the inbox key).</param>
/// <param name="MessageType">The contract's type name (<c>planning.plan-requested</c>); also the routing key.</param>
/// <param name="Payload">The message serialized as JSON.</param>
/// <param name="Headers">Job id, step key and W3C trace context.</param>
/// <param name="OccurredAt">When the producing state change happened.</param>
public sealed record OutgoingEnvelope(
    Guid MessageId,
    string MessageType,
    string Payload,
    IReadOnlyDictionary<string, string> Headers,
    DateTimeOffset OccurredAt);
