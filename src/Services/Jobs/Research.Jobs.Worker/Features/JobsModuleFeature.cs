using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Application.DomainEventHandlers;
using Research.Jobs.Application.EnforceJobDeadlines;
using Research.Jobs.Application.Lifecycle;
using Research.Jobs.Application.Observability;
using Research.Jobs.Application.RecoverStuckJobs;
using Research.Jobs.Domain.Events;
using Research.Jobs.Infrastructure.Persistence;
using Research.Shared.Adapters.Composition;
using Research.Shared.Adapters.Mediation;

namespace Research.Jobs.Worker.Features;

/// <summary>The Jobs coordinator/worker module: repositories, lifecycle, reply gate, domain event handlers and background handlers.</summary>
internal sealed class JobsModuleFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Services.AddSingleton<JobsMetrics>();

        app.Services.AddScoped<IResearchJobRepository, ResearchJobRepository>();
        app.Services.AddScoped<IJobStepRepository, JobStepRepository>();
        app.Services.AddScoped<IResearchReportRepository, ResearchReportRepository>();

        app.Services.AddScoped<StepEmitter>();
        app.Services.AddScoped<JobLifecycle>();
        app.Services.AddScoped<JobReplyGate>();

        app.Services.AddDomainEventHandler<JobCreatedDomainEvent, JobCreatedDomainEventHandler>();
        app.Services.AddDomainEventHandler<JobGatheringStartedDomainEvent, JobGatheringStartedDomainEventHandler>();
        app.Services.AddDomainEventHandler<JobSynthesisStartedDomainEvent, JobSynthesisStartedDomainEventHandler>();
        app.Services.AddDomainEventHandler<JobCompletedDomainEvent, JobCompletedDomainEventHandler>();
        app.Services.AddDomainEventHandler<JobCancelledDomainEvent, JobCancelledDomainEventHandler>();
        app.Services.AddDomainEventHandler<JobFailedDomainEvent, JobFailedDomainEventHandler>();
        app.Services.AddDomainEventHandler<JobExpiredDomainEvent, JobExpiredDomainEventHandler>();

        app.Services.AddCommandHandler<RecoverStuckJobsCommand, int, RecoverStuckJobsHandler>();
        app.Services.AddCommandHandler<EnforceJobDeadlinesCommand, int, EnforceJobDeadlinesHandler>();
    }
}
