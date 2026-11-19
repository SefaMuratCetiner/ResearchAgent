using Research.SharedKernel.Results;

namespace Research.Search.Providers;

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

public sealed record SearchQuery(string Text, int MaxResults);

/// <summary>Hits in the provider's ranking order, and the tokens the search cost (LLM-based providers).</summary>
public sealed record SearchResponse(IReadOnlyList<SearchHit> Hits, long InputTokens, long OutputTokens);

public sealed record SearchHit(string Url, string Title, string? PageAge);

/// <summary>A permanent provider rejection; the step fails with <see cref="Error"/> instead of being retried.</summary>
public sealed class SearchProviderException : Exception
{
    public SearchProviderException()
        : this(SearchErrors.ProviderRefused)
    {
    }

    public SearchProviderException(string message)
        : base(message)
    {
        Error = SearchErrors.ProviderRefused;
    }

    public SearchProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
        Error = SearchErrors.ProviderRefused;
    }

    public SearchProviderException(Error error, string? detail = null)
        : base(detail ?? error?.Title)
    {
        ArgumentNullException.ThrowIfNull(error);
        Error = error;
    }

    public Error Error { get; }
}
