using System.Diagnostics.Metrics;

namespace Research.Knowledge.Application.Observability;

/// <summary>Knowledge instruments (meter <c>Research.Knowledge</c>).</summary>
public sealed class KnowledgeMetrics : IDisposable
{
    public const string MeterName = "Research.Knowledge";

    private readonly Meter _meter;
    private readonly Counter<long> _documents;
    private readonly Counter<long> _passages;
    private readonly Histogram<double> _searchDuration;

    public KnowledgeMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _meter = meterFactory.Create(MeterName);
        _documents = _meter.CreateCounter<long>("research.knowledge.documents", "{document}", "Documents indexed, by outcome (indexed, reused or the error code).");
        _passages = _meter.CreateCounter<long>("research.knowledge.passages", "{passage}", "Passages embedded and stored.");
        _searchDuration = _meter.CreateHistogram<double>("research.knowledge.search.duration", "s", "Time to answer one passage search.");
    }

    public void DocumentProcessed(string outcome) =>
        _documents.Add(1, new KeyValuePair<string, object?>("research.outcome", outcome));

    public void PassagesStored(int count) => _passages.Add(count);

    public void SearchCompleted(TimeSpan duration) => _searchDuration.Record(duration.TotalSeconds);

    public void Dispose() => _meter.Dispose();
}
