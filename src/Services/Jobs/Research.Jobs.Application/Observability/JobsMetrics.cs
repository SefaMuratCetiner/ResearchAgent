using System.Diagnostics.Metrics;
using Research.Jobs.Domain;

namespace Research.Jobs.Application.Observability;

/// <summary>Business instruments of Jobs (§15.4). Never tagged with job ids or question text.</summary>
public sealed class JobsMetrics
{
    public const string MeterName = "Research.Jobs";

    private readonly Counter<long> _created;
    private readonly Counter<long> _finished;
    private readonly Histogram<double> _duration;
    private readonly Histogram<int> _sources;
    private readonly Counter<long> _stepsFinished;
    private readonly Counter<long> _lateResults;
    private readonly Counter<long> _stepsReemitted;

    public JobsMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        _created = meter.CreateCounter<long>("research.jobs.created", description: "Research jobs accepted.");
        _finished = meter.CreateCounter<long>("research.jobs.finished", description: "Research jobs that reached a terminal state.");
        _duration = meter.CreateHistogram<double>("research.job.duration", unit: "s", description: "Time from creation to a terminal state.");
        _sources = meter.CreateHistogram<int>("research.job.sources", description: "Indexed sources per job when gathering ended.");
        _stepsFinished = meter.CreateCounter<long>("research.steps.finished", description: "Steps that finished, by kind and outcome.");
        _lateResults = meter.CreateCounter<long>("research.jobs.late_results", description: "Replies discarded because the job was already terminal.");
        _stepsReemitted = meter.CreateCounter<long>("research.steps.reemitted", description: "Steps re-emitted by recovery after their lease expired.");
    }

    public void Created() => _created.Add(1);

    public void Finished(JobStatus status, string? errorCode, TimeSpan duration)
    {
        _finished.Add(
            1,
            new KeyValuePair<string, object?>("outcome", status.ToString()),
            new KeyValuePair<string, object?>("error.code", errorCode ?? string.Empty));
        _duration.Record(duration.TotalSeconds, new KeyValuePair<string, object?>("outcome", status.ToString()));
    }

    public void GatheringEnded(int sources) => _sources.Record(sources);

    public void StepFinished(StepKind kind, StepStatus status, string? errorCode) =>
        _stepsFinished.Add(
            1,
            new KeyValuePair<string, object?>("step.kind", kind.ToString()),
            new KeyValuePair<string, object?>("outcome", status.ToString()),
            new KeyValuePair<string, object?>("error.code", errorCode ?? string.Empty));

    public void LateResult(string messageType) =>
        _lateResults.Add(1, new KeyValuePair<string, object?>("message.type", messageType));

    public void StepReemitted(StepKind kind) =>
        _stepsReemitted.Add(1, new KeyValuePair<string, object?>("step.kind", kind.ToString()));
}
