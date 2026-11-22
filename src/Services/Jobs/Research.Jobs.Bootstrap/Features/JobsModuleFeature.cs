using Research.Jobs.Application.CancelResearchJob;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Application.Common.Models;
using Research.Jobs.Application.CreateResearchJob;
using Research.Jobs.Application.DomainEventHandlers;
using Research.Jobs.Application.GetResearchJob;
using Research.Jobs.Application.GetResearchReport;
using Research.Jobs.Application.Lifecycle;
using Research.Jobs.Application.Observability;
using Research.Jobs.Domain.Events;
using Research.Jobs.Infrastructure.Persistence;
using Research.Shared.Adapters.Composition;
using Research.Shared.Adapters.Mediation;
using Research.Shared.Adapters.Messaging;
using Research.Shared.Core.Messaging;
using Research.SharedKernel.Results;

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

        app.Services.AddDomainEventHandler<JobCreatedDomainEvent, JobCreatedDomainEventHandler>();
        app.Services.AddDomainEventHandler<JobGatheringStartedDomainEvent, JobGatheringStartedDomainEventHandler>();
        app.Services.AddDomainEventHandler<JobSynthesisStartedDomainEvent, JobSynthesisStartedDomainEventHandler>();
        app.Services.AddDomainEventHandler<JobCompletedDomainEvent, JobCompletedDomainEventHandler>();
        app.Services.AddDomainEventHandler<JobCancelledDomainEvent, JobCancelledDomainEventHandler>();
        app.Services.AddDomainEventHandler<JobFailedDomainEvent, JobFailedDomainEventHandler>();
        app.Services.AddDomainEventHandler<JobExpiredDomainEvent, JobExpiredDomainEventHandler>();

        app.Services.AddCommandHandler<CreateResearchJobCommand, Result<CreateResearchJobResult>, CreateResearchJobHandler>();
        app.Services.AddRequestValidator<CreateResearchJobCommand, CreateResearchJobCommandValidator>();
        app.Services.AddQueryHandler<GetResearchJobQuery, Result<ResearchJobView>, GetResearchJobHandler>();
        app.Services.AddQueryHandler<GetResearchReportQuery, Result<ResearchReportDto>, GetResearchReportHandler>();
        app.Services.AddCommandHandler<CancelResearchJobCommand, Result<ResearchJobDto>, CancelResearchJobHandler>();
    }
}
