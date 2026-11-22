namespace Research.Shared.Messaging;

/// <summary>The header names every integration message carries.</summary>
public static class MessageHeaders
{
    public const string JobId = "research-job-id";
    public const string StepKey = "research-step-key";
    public const string MessageType = "research-message-type";
    public const string OccurredAt = "research-occurred-at";
    public const string Attempt = "research-attempt";
    public const string CorrelationId = "research-correlation-id";
    public const string TraceParent = "traceparent";
    public const string TraceState = "tracestate";
}
