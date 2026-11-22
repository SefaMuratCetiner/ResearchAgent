using Research.Shared.Adapters.Composition;
using Research.Synthesis.Application;

namespace Research.Synthesis.Bootstrap.Features;

/// <summary>The Synthesis module's command handler.</summary>
internal sealed class SynthesisModuleFeature : IHostFeature
{
    public void Register(ServiceHostBuilder app) => app.Services.AddSynthesisApplication();
}
