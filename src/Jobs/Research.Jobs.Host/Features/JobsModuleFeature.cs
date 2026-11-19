using Research.Hosting.Composition;
using Research.Jobs.CancelResearchJob;
using Research.Jobs.CreateResearchJob;
using Research.Jobs.Domain.Ports;
using Research.Jobs.EnforceJobDeadlines;
using Research.Jobs.GetResearchJob;
using Research.Jobs.GetResearchReport;
using Research.Jobs.Lifecycle;
using Research.Jobs.Observability;
using Research.Jobs.Postgres;
using Research.Jobs.RecoverStuckJobs;

namespace Research.Jobs.Host.Features;

/// <summary>The Jobs module: repositories, lifecycle services and one handler per slice. Message handlers are in <see cref="MessagingFeature"/>.</summary>
internal sealed class JobsModuleFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Services.AddSingleton<JobsMetrics>();

        app.Services.AddSingleton<IResearchJobRepository, ResearchJobRepository>();
        app.Services.AddSingleton<IJobStepRepository, JobStepRepository>();
        app.Services.AddSingleton<IResearchReportRepository, ResearchReportRepository>();

        app.Services.AddScoped<StepEmitter>();
        app.Services.AddScoped<JobLifecycle>();
        app.Services.AddScoped<JobReplyGate>();

        app.Services.AddScoped<CreateResearchJobHandler>();
        app.Services.AddScoped<GetResearchJobHandler>();
        app.Services.AddScoped<GetResearchReportHandler>();
        app.Services.AddScoped<CancelResearchJobHandler>();
        app.Services.AddScoped<RecoverStuckJobsHandler>();
        app.Services.AddScoped<EnforceJobDeadlinesHandler>();
    }
}
