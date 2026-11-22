using Research.Shared.Adapters.Composition;
using Research.Synthesis.Application;

namespace Research.Synthesis.Bootstrap.Features;

/// <summary>The Synthesis module's command handler.</summary>
internal sealed class SynthesisModuleFeature : IResearchFeature
{
    public void Register(ResearchBuilder app) => app.Services.AddSynthesisApplication();
}
