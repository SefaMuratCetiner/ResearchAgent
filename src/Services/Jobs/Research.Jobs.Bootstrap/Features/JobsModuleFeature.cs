using Research.Jobs.Application;
using Research.Jobs.Infrastructure;
using Research.Shared.Adapters.Composition;

namespace Research.Jobs.Bootstrap.Features;

/// <summary>The Jobs HTTP API slice registrations: repositories, outbox envelope creation, lifecycle, domain event handlers and API handlers.</summary>
internal sealed class JobsModuleFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Services.AddJobsApiApplication();
        app.Services.AddJobsInfrastructure();
    }
}
