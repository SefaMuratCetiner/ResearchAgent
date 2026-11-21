using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.CancelResearchJob;
using Research.Jobs.CreateResearchJob;
using Research.Jobs.Domain.Events;
using Research.Jobs.DomainEventHandlers;
using Research.Jobs.GetResearchJob;
using Research.Jobs.GetResearchReport;
using Research.Jobs.Infrastructure.Persistence;
using Research.Jobs.Lifecycle;
using Research.Jobs.Observability;
using Research.ServiceDefaults.Composition;
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

        app.Services.AddSingleton<IResearchJobRepository, ResearchJobRepository>();
        app.Services.AddSingleton<IJobStepRepository, JobStepRepository>();
        app.Services.AddSingleton<IResearchReportRepository, ResearchReportRepository>();

        app.Services.AddScoped<StepEmitter>();
        app.Services.AddScoped<JobLifecycle>();

        app.Services.AddScoped<IDomainEventHandler<JobCreatedDomainEvent>, JobCreatedDomainEventHandler>();
        app.Services.AddScoped<IDomainEventHandler<JobCancelledDomainEvent>, JobCancelledDomainEventHandler>();

        app.Services.AddScoped<CreateResearchJobHandler>();
        app.Services.AddScoped<GetResearchJobHandler>();
        app.Services.AddScoped<GetResearchReportHandler>();
        app.Services.AddScoped<CancelResearchJobHandler>();
    }
}
