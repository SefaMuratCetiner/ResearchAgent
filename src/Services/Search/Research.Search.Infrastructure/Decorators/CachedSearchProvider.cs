using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Research.Shared.Ports.Caching;

using Research.Shared.Ports.Search;

namespace Research.Search.Infrastructure.Decorators;

/// <summary>
/// Cache-aside decorator: identical searches (same provider, query and size) within the time-to-live are answered
/// from Valkey at no token cost. A cache outage falls through to the provider. The time-to-live is read on every
/// call, so a reloaded <c>Search:CacheTimeToLive</c> applies without a restart (zero turns the cache off).
/// </summary>
public sealed class CachedSearchProvider(ISearchProvider inner, ICacheStore cache, Func<TimeSpan> timeToLive) : ISearchProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Name => inner.Name;

    public async Task<SearchResponse> SearchAsync(SearchQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var ttl = timeToLive();
        if (ttl <= TimeSpan.Zero)
        {
            return await inner.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        }

        var key = Key(query);
        if (await cache.GetAsync(key, cancellationToken).ConfigureAwait(false) is { } cached)
        {
            try
            {
                if (JsonSerializer.Deserialize<List<SearchHit>>(cached, Json) is { } hits)
                {
                    return new SearchResponse(hits, 0, 0);
                }
            }
            catch (JsonException)
            {
                // A corrupt entry is a miss; the fresh answer overwrites it.
            }
        }

        var response = await inner.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        if (response.Hits.Count > 0)
        {
            await cache.SetAsync(key, JsonSerializer.Serialize(response.Hits, Json), ttl, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    private string Key(SearchQuery query)
    {
        var normalized = inner.Name + "|" + query.Text.Trim().ToUpperInvariant() + "|" + query.MaxResults.ToString(CultureInfo.InvariantCulture);
        return "search:v1:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }
}
