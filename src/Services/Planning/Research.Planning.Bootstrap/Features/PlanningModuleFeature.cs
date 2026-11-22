using Research.Planning.Application;
using Research.Shared.Adapters.Composition;

namespace Research.Planning.Bootstrap.Features;

/// <summary>The Planning module's command handler.</summary>
internal sealed class PlanningModuleFeature : IResearchFeature
{
    public void Register(ResearchBuilder app) => app.Services.AddPlanningApplication();
}
