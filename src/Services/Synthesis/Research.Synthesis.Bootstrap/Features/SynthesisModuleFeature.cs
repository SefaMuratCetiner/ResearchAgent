using Research.Shared.Adapters.Composition;
using Research.Shared.Adapters.Mediation;
using Research.SharedKernel.Results;
using Research.Synthesis.Application.WriteReport;

namespace Research.Synthesis.Bootstrap.Features;

/// <summary>The Synthesis module's command handler.</summary>
internal sealed class SynthesisModuleFeature : IResearchFeature
{
    public void Register(ResearchBuilder app) =>
        app.Services.AddCommandHandler<WriteReportCommand, Result<WrittenReport>, WriteReportHandler>();
}
