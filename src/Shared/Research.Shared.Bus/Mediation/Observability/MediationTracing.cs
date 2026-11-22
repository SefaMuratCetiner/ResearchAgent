using System.Diagnostics;

namespace Research.Shared.Bus.Mediation.Observability;

/// <summary>OpenTelemetry activity source for the unified mediation pipeline (commands, queries, messages, domain events).</summary>
public static class MediationTracing
{
    public const string SourceName = "Research.Mediation";

    private static readonly ActivitySource Source = new(SourceName);

    public static Activity? StartRequest(string requestName, string kind)
    {
        var activity = Source.StartActivity($"{kind} {requestName}", ActivityKind.Internal);
        activity?.SetTag("mediation.request.name", requestName);
        activity?.SetTag("mediation.request.kind", kind);
        return activity;
    }

    public static Activity? StartEvent(string eventName)
    {
        var activity = Source.StartActivity($"event {eventName}", ActivityKind.Internal);
        activity?.SetTag("mediation.event.name", eventName);
        return activity;
    }
}
