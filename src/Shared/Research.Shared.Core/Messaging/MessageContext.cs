namespace Research.Shared.Core.Messaging;

/// <summary>What a handler knows about the delivery it is processing.</summary>
/// <param name="MessageId">The envelope's id; pass it to <see cref="IInbox"/>.</param>
/// <param name="MessageType">The contract type name.</param>
/// <param name="Attempt">1 on the first delivery, increased on every retry.</param>
/// <param name="OccurredAt">When the producer's state change happened.</param>
/// <param name="Consumer">The consuming queue's name; the inbox key together with the message id.</param>
public sealed record MessageContext(
    Guid MessageId,
    string MessageType,
    int Attempt,
    DateTimeOffset OccurredAt,
    string Consumer);
