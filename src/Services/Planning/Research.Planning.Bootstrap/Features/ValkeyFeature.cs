using Research.Providers.Valkey;
using Research.Shared.Adapters.Composition;

namespace Research.Planning.Bootstrap.Features;

/// <summary>
/// The plan cache on Valkey (Aspire connection <c>cache</c>). Aspire's own check is off: it would report the optional
/// cache as Unhealthy; ours reports Degraded.
/// </summary>
internal sealed class ValkeyFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Host.AddRedisClient("cache", static settings => settings.DisableHealthChecks = true);
        app.Services.AddValkeyCache();
    }
}
