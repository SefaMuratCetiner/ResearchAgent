using Research.Shared.Adapters.Composition;
using Research.Planning.Application.CreatePlan;

namespace Research.Planning.Bootstrap.Features;

/// <summary>The Planning module's handler.</summary>
internal sealed class PlanningModuleFeature : IResearchFeature
{
    public void Register(ResearchBuilder app) => app.Services.AddScoped<CreatePlanHandler>();
}
