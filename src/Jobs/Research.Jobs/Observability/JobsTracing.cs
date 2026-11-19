using System.Diagnostics;

namespace Research.Jobs.Observability;

/// <summary>Spans of Jobs' own work (worker runs). Message handling spans come from Research.Messaging.</summary>
public static class JobsTracing
{
    public const string SourceName = "Research.Jobs";

    public static ActivitySource Source { get; } = new(SourceName);

    /// <summary>Starts a new root span for one worker run, so runs never join a request's trace.</summary>
    public static Activity? StartWorkerRun(string name)
    {
        var previous = Activity.Current;
        Activity.Current = null;
        var activity = Source.StartActivity(name, ActivityKind.Internal);
        if (activity is null)
        {
            Activity.Current = previous;
        }

        return activity;
    }
}
