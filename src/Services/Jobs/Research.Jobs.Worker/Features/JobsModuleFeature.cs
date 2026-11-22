using Research.Jobs.Application;
using Research.Jobs.Infrastructure;
using Research.Shared.Adapters.Composition;

namespace Research.Jobs.Worker.Features;

/// <summary>The Jobs coordinator/worker module: repositories, lifecycle, reply gate, domain event handlers and background handlers.</summary>
internal sealed class JobsModuleFeature : IHostFeature
{
    public void Register(ServiceHostBuilder app)
    {
        app.Services.AddJobsWorkerApplication();
        app.Services.AddJobsInfrastructure();
    }
}
