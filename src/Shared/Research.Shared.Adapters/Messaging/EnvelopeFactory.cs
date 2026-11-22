using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Research.Contracts;
using Research.Shared.Adapters.Messaging.Tracing;
using Research.Shared.Messaging;

namespace Research.Shared.Adapters.Messaging;

/// <summary>
/// Turns a contract into an <see cref="OutgoingEnvelope"/>: a UUID v7 message id, the JSON payload, and the headers
/// (job id, step key, type, time, attempt 1, and the current trace context).
/// </summary>
public sealed class EnvelopeFactory(TimeProvider timeProvider) : IEnvelopeFactory
{
    public OutgoingEnvelope Create<TMessage>(TMessage message)
        where TMessage : IIntegrationMessage
    {
        ArgumentNullException.ThrowIfNull(message);
        return Recreate(TMessage.MessageType, JsonSerializer.Serialize(message, MessageJson.Options), message.JobId, message.StepKey);
    }

    /// <summary>
    /// A new envelope (new message id, current trace context) for a payload serialized earlier: how Jobs re-emits a
    /// stuck step from the request it stored when the step started.
    /// </summary>
    public OutgoingEnvelope Recreate(string messageType, string payload, Guid jobId, string stepKey)
    {
        var occurredAt = timeProvider.GetUtcNow();
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessageHeaders.JobId] = jobId.ToString("D"),
            [MessageHeaders.StepKey] = stepKey,
            [MessageHeaders.MessageType] = messageType,
            [MessageHeaders.OccurredAt] = occurredAt.ToString("O", CultureInfo.InvariantCulture),
            [MessageHeaders.Attempt] = "1",
        };
        TraceContextHeaders.Inject(Activity.Current, headers);

        return new OutgoingEnvelope(Guid.CreateVersion7(occurredAt), messageType, payload, headers, occurredAt);
    }
}
