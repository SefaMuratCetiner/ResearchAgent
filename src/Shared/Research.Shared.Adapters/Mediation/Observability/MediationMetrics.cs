using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Research.Shared.Adapters.Mediation.Observability;

/// <summary>OpenTelemetry metrics for every request and domain event flowing through the mediation pipeline.</summary>
public sealed class MediationMetrics : IDisposable
{
    public const string MeterName = "Research.Mediation";

    private readonly Meter _meter;
    private readonly Counter<long> _requests;
    private readonly Histogram<double> _requestDuration;
    private readonly Counter<long> _events;
    private readonly Histogram<double> _eventDuration;

    public MediationMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _meter = meterFactory.Create(MeterName);
        _requests = _meter.CreateCounter<long>("research.mediation.requests", description: "Requests processed by the mediation pipeline.");
        _requestDuration = _meter.CreateHistogram<double>("research.mediation.duration", "s", "Duration of requests processed by the mediation pipeline.");
        _events = _meter.CreateCounter<long>("research.mediation.events", description: "Domain events published through the mediation pipeline.");
        _eventDuration = _meter.CreateHistogram<double>("research.mediation.event_duration", "s", "Duration of domain event publishing.");
    }

    public void RecordRequest(string requestName, string kind, string outcome, TimeSpan duration)
    {
        var tags = new TagList
        {
            { "request", requestName },
            { "kind", kind },
            { "outcome", outcome },
        };
        _requests.Add(1, tags);
        _requestDuration.Record(duration.TotalSeconds, tags);
    }

    public void RecordEvent(string eventName, string outcome, TimeSpan duration)
    {
        var tags = new TagList
        {
            { "event", eventName },
            { "outcome", outcome },
        };
        _events.Add(1, tags);
        _eventDuration.Record(duration.TotalSeconds, tags);
    }

    public void Dispose() => _meter.Dispose();
}
