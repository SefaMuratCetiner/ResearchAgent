using Research.Shared.Adapters.Mediation;

namespace Research.Shared.Adapters.Composition;

/// <summary>Registers the unified mediation pipeline (commands, queries, integration messages, domain events, and behaviors).</summary>
public sealed class MediationFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Services.AddMediation();
    }
}
