using System.Diagnostics.Metrics;

namespace Research.Search.Application.Observability;

/// <summary>Search instruments, tagged by provider only.</summary>
public sealed class SearchMetrics
{
    public const string MeterName = "Research.Search";

    private readonly Histogram<double> _duration;
    private readonly Histogram<int> _hits;

    public SearchMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        _duration = meter.CreateHistogram<double>("research.search.duration", unit: "s", description: "Time per search, cache hits included.");
        _hits = meter.CreateHistogram<int>("research.search.hits", description: "Distinct URLs returned per search.");
    }

    public void Searched(string provider, TimeSpan elapsed, int hits)
    {
        var tag = new KeyValuePair<string, object?>("provider", provider);
        _duration.Record(elapsed.TotalSeconds, tag);
        _hits.Record(hits, tag);
    }
}
