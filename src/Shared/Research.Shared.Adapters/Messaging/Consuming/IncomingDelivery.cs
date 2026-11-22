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
