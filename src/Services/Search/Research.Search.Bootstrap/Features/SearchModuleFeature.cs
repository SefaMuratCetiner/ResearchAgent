using Microsoft.Extensions.Options;
using Research.Search.Application;
using Research.Search.Application.RunSearch;
using Research.Search.Infrastructure.Decorators;
using Research.Shared.Adapters.Composition;
using Research.Shared.Adapters.Mediation;
using Research.Shared.Ports.Caching;
using Research.Shared.Ports.Search;
using Research.SharedKernel.Results;

namespace Research.Search.Bootstrap.Features;

/// <summary>
/// The Search module. The provider is decorated twice: the limiter wraps the provider, and the cache wraps both, so
/// cache hits never wait for a slot. Both decorators read <see cref="SearchOptions"/> on every call, so a reload applies
/// without a restart.
/// </summary>
internal sealed class SearchModuleFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Services.AddSingleton<SearchMetrics>();

        app.Decorate<ISearchProvider>(static (provider, inner) =>
        {
            var options = provider.GetRequiredService<IOptionsMonitor<SearchOptions>>();
            return new RateLimitedSearchProvider(inner, () => options.CurrentValue.MaxConcurrentSearches);
        });
        app.Decorate<ISearchProvider>(static (provider, inner) =>
        {
            var options = provider.GetRequiredService<IOptionsMonitor<SearchOptions>>();
            return new CachedSearchProvider(inner, provider.GetRequiredService<ICacheStore>(), () => options.CurrentValue.CacheTimeToLive);
        });

        app.Services.AddCommandHandler<RunSearchCommand, Result<SearchResponse>, RunSearchHandler>();
    }
}
