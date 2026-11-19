using Research.SharedKernel.Concurrency;

namespace Research.Search.Providers;

/// <summary>
/// Caps concurrent provider calls per replica; callers beyond the cap wait. The cap is read on every call, so a
/// reloaded <c>Search:MaxConcurrentSearches</c> applies without a restart.
/// </summary>
public sealed class RateLimitedSearchProvider(ISearchProvider inner, Func<int> maxConcurrentSearches) : ISearchProvider, IDisposable
{
    private readonly ResizableConcurrencyLimit _slots = new(maxConcurrentSearches);

    public string Name => inner.Name;

    public async Task<SearchResponse> SearchAsync(SearchQuery query, CancellationToken cancellationToken)
    {
        using (await _slots.AcquireAsync(cancellationToken).ConfigureAwait(false))
        {
            return await inner.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose() => _slots.Dispose();
}
