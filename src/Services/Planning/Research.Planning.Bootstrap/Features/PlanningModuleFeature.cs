using Research.Planning.Application.CreatePlan;
using Research.Shared.Adapters.Composition;
using Research.Shared.Adapters.Mediation;
using Research.SharedKernel.Results;

namespace Research.Planning.Bootstrap.Features;

/// <summary>The Planning module's command handler.</summary>
internal sealed class PlanningModuleFeature : IResearchFeature
{
    public void Register(ResearchBuilder app) =>
        app.Services.AddCommandHandler<CreatePlanCommand, Result<ResearchPlan>, CreatePlanHandler>();
}
