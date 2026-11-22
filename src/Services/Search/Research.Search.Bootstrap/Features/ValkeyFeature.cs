using Microsoft.Extensions.Diagnostics.HealthChecks;
using Research.Shared.Adapters.Composition;
using Research.Providers.Valkey;
using Research.Shared.Ports.Caching;

namespace Research.Search.Bootstrap.Features;

/// <summary>
/// The search cache on Valkey (Aspire connection <c>cache</c>). Aspire's own check is off: it would report the optional
/// cache as Unhealthy; ours reports Degraded.
/// </summary>
internal sealed class ValkeyFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Host.AddRedisClient("cache", static settings => settings.DisableHealthChecks = true);
        app.Services.AddSingleton<ICacheStore, ValkeyCacheStore>();
        app.Services.AddHealthChecks().AddCheck<ValkeyHealthCheck>("cache", failureStatus: HealthStatus.Degraded);
    }
}
