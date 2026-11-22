using Research.Fetching.Application;
using Research.Fetching.Infrastructure;
using Research.Shared.Core.Hosting;

namespace Research.Fetching.Bootstrap.Features;

/// <summary>
/// The Fetching module: the SSRF-guarded download client, robots policy, per-domain throttle, extractor, repository and handler.
/// </summary>
internal sealed class FetchingModuleFeature : IHostFeature
{
    public void Register(ServiceHostBuilder app)
    {
        app.Services.AddFetchingApplication();
        app.Services.AddFetchingInfrastructure();
    }
}
