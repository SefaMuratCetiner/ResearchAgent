using Research.Shared.Core.Hosting;

namespace Research.Shared.Bus;

/// <summary>Registers the unified bus mediation pipeline (commands, queries, integration messages, domain events, and behaviors).</summary>
public sealed class BusFeature : IHostFeature
{
    public void Register(ServiceHostBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Services.AddMediation();
    }
}
