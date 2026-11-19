using Research.Hosting.Composition;
using Research.Planning.CreatePlan;

namespace Research.Planning.Host.Features;

/// <summary>The Planning module's handler.</summary>
internal sealed class PlanningModuleFeature : IResearchFeature
{
    public void Register(ResearchBuilder app) => app.Services.AddScoped<CreatePlanHandler>();
}
