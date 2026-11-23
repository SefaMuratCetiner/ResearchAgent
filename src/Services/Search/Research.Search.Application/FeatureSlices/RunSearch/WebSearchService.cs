using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Search.Application.Observability;
using Research.Search.Domain.Policies;
using Research.Shared.Contracts.Search;
using Research.Shared.Core.Logging;
using Research.Shared.Core.Search;
using Research.Shared.Core.Security;
using Research.Shared.Kernel.Results;

namespace Research.Search.Application.RunSearch;

/// <summary>
/// Runs one search through the decorated provider, canonicalizes and deduplicates the URLs, and logs the query and
/// every hit under <see cref="ContentLogCategories.Search"/>. Permanent provider rejections become failures.
/// </summary>
public sealed partial class WebSearchService
{
    private readonly ISearchProvider _provider;
    private readonly SearchMetrics _metrics;
    private readonly IOptionsSnapshot<SearchOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _contentLogger;

    public WebSearchService(
        ISearchProvider provider,
        SearchMetrics metrics,
        IOptionsSnapshot<SearchOptions> options,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _provider = provider;
        _metrics = metrics;
        _options = options;
        _timeProvider = timeProvider;
        _contentLogger = loggerFactory.CreateLogger(ContentLogCategories.Search);
    }

    public async Task<Result<SearchResponse>> SearchAsync(SearchRequested message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var maxResults = Math.Clamp(message.MaxResults, 1, _options.Value.MaxResults);
        var started = _timeProvider.GetTimestamp();

        SearchResponse response;
        try
        {
            response = await _provider.SearchAsync(new SearchQuery(message.Query, maxResults), cancellationToken).ConfigureAwait(false);
        }
        catch (SearchProviderException exception)
        {
            LogRejected(_contentLogger, message.Query, exception.Error.Code);
            return Result<SearchResponse>.Failure(exception.Error);
        }

        var hits = new List<SearchHit>(maxResults);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var hit in response.Hits)
        {
            // Titles are web text: one clean line, so they cannot smuggle instructions or break a prompt section.
            if (UrlCanonicalizer.Canonicalize(hit.Url) is { } url && UntrustedText.IsWebUrl(url) && seen.Add(url))
            {
                var title = UntrustedText.SingleLine(hit.Title);
                hits.Add(hit with
                {
                    Url = url,
                    Title = title.Length == 0 ? url : title,
                    PageAge = hit.PageAge is null ? null : UntrustedText.SingleLine(hit.PageAge, 64),
                });
                if (hits.Count == maxResults)
                {
                    break;
                }
            }
        }

        _metrics.Searched(_provider.Name, _timeProvider.GetElapsedTime(started), hits.Count);
        LogQuery(_contentLogger, message.Query, hits.Count, _provider.Name);
        foreach (var hit in hits)
        {
            LogHit(_contentLogger, message.Query, hit.Url, hit.Title, hit.PageAge);
        }

        return Result<SearchResponse>.Success(response with { Hits = hits });
    }

    [LoggerMessage(EventId = 4000, Level = LogLevel.Information, Message = "Search {Query} returned {HitCount} results from {Provider}")]
    private static partial void LogQuery(ILogger logger, string query, int hitCount, string provider);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Information, Message = "Search {Query} result {Url} {Title} (age {PageAge})")]
    private static partial void LogHit(ILogger logger, string query, string url, string title, string? pageAge);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Warning, Message = "Search {Query} was rejected by the provider: {ErrorCode}")]
    private static partial void LogRejected(ILogger logger, string query, string errorCode);
}
