using Research.Search.Application;
using Research.Search.Infrastructure;
using Research.Shared.Adapters.Composition;

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
        app.Services.AddSearchApplication();
        app.Services.AddSearchInfrastructure();
    }
}
