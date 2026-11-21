using System.Diagnostics.Metrics;

namespace Research.Fetching.Application;

/// <summary>Fetching instruments (meter <c>Research.Fetching</c>).</summary>
public sealed class FetchingMetrics : IDisposable
{
    public const string MeterName = "Research.Fetching";

    private readonly Meter _meter;
    private readonly Counter<long> _pages;
    private readonly Histogram<long> _bytes;
    private readonly Histogram<double> _duration;
    private readonly Counter<long> _injectionSignals;

    public FetchingMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _meter = meterFactory.Create(MeterName);
        _pages = _meter.CreateCounter<long>("research.fetching.pages", "{page}", "Pages processed, by outcome (ok or the error code).");
        _bytes = _meter.CreateHistogram<long>("research.fetching.page.size", "By", "Downloaded body size.");
        _duration = _meter.CreateHistogram<double>("research.fetching.duration", "s", "Time to download and extract one page.");
        _injectionSignals = _meter.CreateCounter<long>("research.fetching.injection_signals", "{signal}", "Prompt-injection signals found in page text, by signal.");
    }

    public void PageProcessed(string outcome, TimeSpan duration)
    {
        _pages.Add(1, new KeyValuePair<string, object?>("research.outcome", outcome));
        _duration.Record(duration.TotalSeconds, new KeyValuePair<string, object?>("research.outcome", outcome));
    }

    public void PageSize(long bytes) => _bytes.Record(bytes);

    /// <summary>One count per prompt-injection signal found in a page's text (OWASP ASI10: watch for it).</summary>
    public void InjectionSignals(IReadOnlyList<string> signals)
    {
        ArgumentNullException.ThrowIfNull(signals);
        foreach (var signal in signals)
        {
            _injectionSignals.Add(1, new KeyValuePair<string, object?>("research.signal", signal));
        }
    }

    public void Dispose() => _meter.Dispose();
}
