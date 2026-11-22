using Research.Providers.Valkey;
using Research.Shared.Core.Hosting;

namespace Research.Search.Bootstrap.Features;

/// <summary>
/// The search cache on Valkey (Aspire connection <c>cache</c>). Aspire's own check is off: it would report the optional
/// cache as Unhealthy; ours reports Degraded.
/// </summary>
internal sealed class ValkeyFeature : IHostFeature
{
    public void Register(ServiceHostBuilder app)
    {
        app.Host.AddRedisClient("cache", static settings => settings.DisableHealthChecks = true);
        app.Services.AddValkeyCache();
    }
}
