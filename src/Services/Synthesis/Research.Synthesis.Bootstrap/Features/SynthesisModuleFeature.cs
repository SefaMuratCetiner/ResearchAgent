using Research.Shared.Adapters.Composition;
using Research.Synthesis.Application.WriteReport;

namespace Research.Synthesis.Bootstrap.Features;

/// <summary>The Synthesis module's handler.</summary>
internal sealed class SynthesisModuleFeature : IResearchFeature
{
    public void Register(ResearchBuilder app) => app.Services.AddScoped<WriteReportHandler>();
}
