namespace Research.Shared.Ports.Search;

/// <summary>
/// A web search engine (strategy). Transient failures throw any exception (the pipeline retries); a permanent
/// rejection throws <see cref="SearchProviderException"/> with a <c>search.*</c> error.
/// </summary>
public interface ISearchProvider
{
    /// <summary>A short, stable name for metrics and logs (<c>anthropic-web-search</c>).</summary>
    string Name { get; }

    Task<SearchResponse> SearchAsync(SearchQuery query, CancellationToken cancellationToken);
}
