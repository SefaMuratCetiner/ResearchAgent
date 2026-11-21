using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Application.CancelResearchJob;
using Research.Jobs.Application.CreateResearchJob;
using Research.Jobs.Domain.Events;
using Research.Jobs.Application.DomainEventHandlers;
using Research.Jobs.Application.GetResearchJob;
using Research.Jobs.Application.GetResearchReport;
using Research.Jobs.Infrastructure.Persistence;
using Research.Jobs.Application.Lifecycle;
using Research.Jobs.Application.Observability;
using Research.Shared.Adapters.Composition;
using Research.Shared.Adapters.Messaging;
using Research.Shared.Core.Events;
using Research.Shared.Core.Messaging;

namespace Research.Jobs.Bootstrap.Features;

/// <summary>The Jobs HTTP API slice registrations: repositories, outbox envelope creation, lifecycle, domain event handlers and API handlers.</summary>
internal sealed class JobsModuleFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Services.AddSingleton<JobsMetrics>();
        app.Services.AddSingleton<IEnvelopeFactory, EnvelopeFactory>();

        app.Services.AddScoped<IResearchJobRepository, ResearchJobRepository>();
        app.Services.AddScoped<IJobStepRepository, JobStepRepository>();
        app.Services.AddScoped<IResearchReportRepository, ResearchReportRepository>();

        app.Services.AddScoped<StepEmitter>();
        app.Services.AddScoped<JobLifecycle>();

        app.Services.AddScoped<IDomainEventHandler<JobCreatedDomainEvent>, JobCreatedDomainEventHandler>();
        app.Services.AddScoped<IDomainEventHandler<JobGatheringStartedDomainEvent>, JobGatheringStartedDomainEventHandler>();
        app.Services.AddScoped<IDomainEventHandler<JobSynthesisStartedDomainEvent>, JobSynthesisStartedDomainEventHandler>();
        app.Services.AddScoped<IDomainEventHandler<JobCompletedDomainEvent>, JobCompletedDomainEventHandler>();
        app.Services.AddScoped<IDomainEventHandler<JobCancelledDomainEvent>, JobCancelledDomainEventHandler>();
        app.Services.AddScoped<IDomainEventHandler<JobFailedDomainEvent>, JobFailedDomainEventHandler>();
        app.Services.AddScoped<IDomainEventHandler<JobExpiredDomainEvent>, JobExpiredDomainEventHandler>();

        app.Services.AddScoped<CreateResearchJobHandler>();
        app.Services.AddScoped<GetResearchJobHandler>();
        app.Services.AddScoped<GetResearchReportHandler>();
        app.Services.AddScoped<CancelResearchJobHandler>();
    }
}
