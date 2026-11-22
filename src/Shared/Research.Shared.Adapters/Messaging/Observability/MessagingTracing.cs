using System.Diagnostics;

namespace Research.Shared.Adapters.Messaging.Observability;

/// <summary>Spans of the messaging pipeline. Every span carries <see cref="JobIdTag"/> so a job's hops can be found.</summary>
public static class MessagingTracing
{
    public const string SourceName = "Research.Messaging";

    public const string JobIdTag = "research.job.id";
    public const string StepKeyTag = "research.step.key";
    public const string ErrorCodeTag = "research.error.code";

    public static ActivitySource Source { get; } = new(SourceName);

    /// <summary>
    /// Starts the consumer span of one delivery as a new trace linked to the producer's span (one trace per hop),
    /// which keeps long-running jobs out of a single, ever-growing trace.
    /// </summary>
    public static Activity? StartConsume(string consumer, string messageType, ActivityContext producer)
    {
        var previous = Activity.Current;
        Activity.Current = null;
        try
        {
            ActivityLink[]? links = producer == default ? null : [new ActivityLink(producer)];
            var activity = Source.StartActivity(
                consumer + " process",
                ActivityKind.Consumer,
                parentContext: default,
                tags: null,
                links: links);
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.operation.type", "process");
            activity?.SetTag("messaging.destination.name", consumer);
            activity?.SetTag("research.message.type", messageType);
            return activity;
        }
        finally
        {
            if (Activity.Current is null)
            {
                Activity.Current = previous;
            }
        }
    }
}
