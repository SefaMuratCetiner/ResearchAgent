using Research.Shared.Adapters.Mediation;

namespace Research.Shared.Adapters.Composition;

/// <summary>Registers the unified mediation pipeline (commands, queries, integration messages, domain events, and behaviors).</summary>
public sealed class MediationFeature : IHostFeature
{
    public void Register(ServiceHostBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Services.AddMediation();
    }
}
