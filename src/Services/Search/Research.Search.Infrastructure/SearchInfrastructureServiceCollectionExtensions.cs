using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Research.Search.Application;
using Research.Search.Infrastructure.Decorators;
using Research.Shared.Core.Hosting;
using Research.Shared.Core.Caching;
using Research.Shared.Core.Search;

namespace Research.Search.Infrastructure;

/// <summary>DI registrations for the Search Infrastructure layer.</summary>
public static class SearchInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Decorates the registered <see cref="ISearchProvider"/> with concurrency rate limiting (inner) and Valkey
    /// cache-aside (outer), so cache hits never wait for a concurrency slot.
    /// </summary>
    public static IServiceCollection AddSearchInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Decorate<ISearchProvider>(static (provider, inner) =>
        {
            var options = provider.GetRequiredService<IOptionsMonitor<SearchOptions>>();
            return new RateLimitedSearchProvider(inner, () => options.CurrentValue.MaxConcurrentSearches);
        });

        services.Decorate<ISearchProvider>(static (provider, inner) =>
        {
            var options = provider.GetRequiredService<IOptionsMonitor<SearchOptions>>();
            return new CachedSearchProvider(inner, provider.GetRequiredService<ICacheStore>(), () => options.CurrentValue.CacheTimeToLive);
        });

        return services;
    }
}
