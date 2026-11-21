using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Domain.Events;
using Research.Jobs.DomainEventHandlers;
using Research.Jobs.EnforceJobDeadlines;
using Research.Jobs.Infrastructure.Persistence;
using Research.Jobs.Lifecycle;
using Research.Jobs.Observability;
using Research.Jobs.RecoverStuckJobs;
using Research.ServiceDefaults.Composition;
using Research.Shared.Core.Events;

namespace Research.Jobs.Worker.Features;

/// <summary>The Jobs coordinator/worker module: repositories, lifecycle, reply gate, domain event handlers and background handlers.</summary>
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

        app.Services.AddScoped<IDomainEventHandler<JobGatheringStartedDomainEvent>, JobGatheringStartedDomainEventHandler>();
        app.Services.AddScoped<IDomainEventHandler<JobSynthesisStartedDomainEvent>, JobSynthesisStartedDomainEventHandler>();
        app.Services.AddScoped<IDomainEventHandler<JobCompletedDomainEvent>, JobCompletedDomainEventHandler>();
        app.Services.AddScoped<IDomainEventHandler<JobCancelledDomainEvent>, JobCancelledDomainEventHandler>();
        app.Services.AddScoped<IDomainEventHandler<JobFailedDomainEvent>, JobFailedDomainEventHandler>();
        app.Services.AddScoped<IDomainEventHandler<JobExpiredDomainEvent>, JobExpiredDomainEventHandler>();

        app.Services.AddScoped<RecoverStuckJobsHandler>();
        app.Services.AddScoped<EnforceJobDeadlinesHandler>();
    }
}
