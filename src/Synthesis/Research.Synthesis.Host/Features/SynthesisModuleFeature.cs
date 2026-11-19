using Research.Hosting.Composition;
using Research.Synthesis.WriteReport;

namespace Research.Synthesis.Host.Features;

/// <summary>The Synthesis module's handler.</summary>
internal sealed class SynthesisModuleFeature : IResearchFeature
{
    public void Register(ResearchBuilder app) => app.Services.AddScoped<WriteReportHandler>();
}
