using Research.Search.Application;
using Research.Search.Infrastructure;
using Research.Shared.Core.Hosting;

namespace Research.Search.Bootstrap.Features;

/// <summary>
/// The Search module. The provider is decorated twice: the limiter wraps the provider, and the cache wraps both, so
/// cache hits never wait for a slot. Both decorators read <see cref="SearchOptions"/> on every call, so a reload applies
/// without a restart.
/// </summary>
internal sealed class SearchModuleFeature : IHostFeature
{
    public void Register(ServiceHostBuilder app)
    {
        app.Services.AddSearchApplication();
        app.Services.AddSearchInfrastructure();
    }
}
