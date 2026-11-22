using System.Diagnostics.Metrics;

namespace Research.Shared.Core.Llm.Observability;

/// <summary>LLM instruments: duration, tokens and throttling, tagged by model and operation only.</summary>
public sealed class LlmMetrics
{
    public const string MeterName = "Research.Llm";

    private readonly Histogram<double> _duration;
    private readonly Counter<long> _tokens;
    private readonly Counter<long> _throttled;
    private readonly Counter<long> _refusals;

    public LlmMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        _duration = meter.CreateHistogram<double>("research.llm.duration", unit: "s", description: "LLM request duration.");
        _tokens = meter.CreateCounter<long>("research.llm.tokens", unit: "{token}", description: "Billed tokens.");
        _throttled = meter.CreateCounter<long>("research.llm.throttled", description: "Requests rejected by rate limits.");
        _refusals = meter.CreateCounter<long>("research.llm.refusals", description: "Requests the model refused.");
    }

    public void Completed(string model, string operation, TimeSpan elapsed, long inputTokens, long outputTokens)
    {
        _duration.Record(
            elapsed.TotalSeconds,
            new KeyValuePair<string, object?>("model", model),
            new KeyValuePair<string, object?>("operation", operation));
        _tokens.Add(inputTokens, new KeyValuePair<string, object?>("model", model), new KeyValuePair<string, object?>("direction", "input"));
        _tokens.Add(outputTokens, new KeyValuePair<string, object?>("model", model), new KeyValuePair<string, object?>("direction", "output"));
    }

    public void Throttled(string model) => _throttled.Add(1, new KeyValuePair<string, object?>("model", model));

    public void Refused(string model) => _refusals.Add(1, new KeyValuePair<string, object?>("model", model));
}
