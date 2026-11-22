using Research.Jobs.Application;
using Research.Jobs.Infrastructure;
using Research.Shared.Core.Hosting;

namespace Research.Jobs.Bootstrap.Features;

/// <summary>The Jobs HTTP API slice registrations: repositories, outbox envelope creation, lifecycle, domain event handlers and API handlers.</summary>
internal sealed class JobsModuleFeature : IHostFeature
{
    public void Register(ServiceHostBuilder app)
    {
        app.Services.AddJobsApiApplication();
        app.Services.AddJobsInfrastructure();
    }
}
