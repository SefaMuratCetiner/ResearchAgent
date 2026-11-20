using System.Diagnostics;

namespace Research.Shared.Adapters.Messaging.Tracing;

/// <summary>W3C trace context in and out of message headers, through the runtime's configured propagator.</summary>
public static class TraceContextHeaders
{
    public static void Inject(Activity? activity, IDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        if (activity is null)
        {
            return;
        }

        DistributedContextPropagator.Current.Inject(
            activity,
            headers,
            static (carrier, name, value) =>
            {
                if (carrier is IDictionary<string, string> target && value is not null)
                {
                    target[name] = value;
                }
            });
    }

    /// <returns>The producer's context, or <c>default</c> when the headers carry none or it does not parse.</returns>
    public static ActivityContext Extract(IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        DistributedContextPropagator.Current.ExtractTraceIdAndState(
            headers,
            static (object? carrier, string name, out string? value, out IEnumerable<string>? values) =>
            {
                values = null;
                value = null;
                if (carrier is IReadOnlyDictionary<string, string> source && source.TryGetValue(name, out var found))
                {
                    value = found;
                }
            },
            out var traceParent,
            out var traceState);

        return ActivityContext.TryParse(traceParent, traceState, isRemote: true, out var context) ? context : default;
    }
}
