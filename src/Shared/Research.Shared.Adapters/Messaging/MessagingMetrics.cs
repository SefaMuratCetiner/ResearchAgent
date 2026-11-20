using System.Diagnostics.Metrics;

namespace Research.Shared.Adapters.Messaging;

/// <summary>Messaging instruments. Tags are bounded: queue and outcome only, never job ids.</summary>
public sealed class MessagingMetrics
{
    public const string MeterName = "Research.Messaging";

    private readonly Histogram<double> _handlerDuration;
    private readonly Counter<long> _deadLettered;
    private readonly Counter<long> _outboxPublished;
    private readonly Counter<long> _outboxFailures;

    public MessagingMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        _handlerDuration = meter.CreateHistogram<double>(
            "research.messaging.handler.duration",
            unit: "s",
            description: "Time spent handling one delivery.");
        _deadLettered = meter.CreateCounter<long>(
            "research.messaging.dead_lettered",
            description: "Deliveries moved to a dead-letter queue.");
        _outboxPublished = meter.CreateCounter<long>(
            "research.outbox.published",
            description: "Outbox messages published and confirmed.");
        _outboxFailures = meter.CreateCounter<long>(
            "research.outbox.failures",
            description: "Outbox dispatcher runs that failed.");
    }

    public void RecordHandled(string queue, string outcome, TimeSpan elapsed) =>
        _handlerDuration.Record(
            elapsed.TotalSeconds,
            new KeyValuePair<string, object?>("queue", queue),
            new KeyValuePair<string, object?>("outcome", outcome));

    public void DeadLettered(string queue) =>
        _deadLettered.Add(1, new KeyValuePair<string, object?>("queue", queue));

    public void OutboxPublished(int count)
    {
        if (count > 0)
        {
            _outboxPublished.Add(count);
        }
    }

    public void OutboxFailed() => _outboxFailures.Add(1);
}
