using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Research.Jobs.Application.CancelResearchJob;
using Research.Jobs.Application.Common.Models;
using Research.Jobs.Application.CreateResearchJob;
using Research.Jobs.Application.DomainEventHandlers;
using Research.Jobs.Application.EnforceJobDeadlines;
using Research.Jobs.Application.GetResearchJob;
using Research.Jobs.Application.GetResearchReport;
using Research.Jobs.Application.Lifecycle;
using Research.Jobs.Application.Observability;
using Research.Jobs.Application.RecoverStuckJobs;
using Research.Jobs.Domain.Events;
using Research.Shared.Core.Events;
using Research.Shared.Core.Mediation;
using Research.SharedKernel.Results;

namespace Research.Jobs.Application;

/// <summary>DI registrations for the Jobs Application layer (API host and Worker host).</summary>
public static class JobsApplicationServiceCollectionExtensions
{
    /// <summary>Registers Jobs API handlers, validators, lifecycle orchestration, and domain event handlers.</summary>
    public static IServiceCollection AddJobsApiApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddJobsApplicationCore();

        services.AddScoped<IRequestHandler<CreateResearchJobCommand, Result<CreateResearchJobResult>>, CreateResearchJobHandler>();
        services.AddScoped<IRequestValidator<CreateResearchJobCommand>, CreateResearchJobCommandValidator>();
        services.AddScoped<IRequestHandler<GetResearchJobQuery, Result<ResearchJobView>>, GetResearchJobHandler>();
        services.AddScoped<IRequestHandler<GetResearchReportQuery, Result<ResearchReportDto>>, GetResearchReportHandler>();
        services.AddScoped<IRequestHandler<CancelResearchJobCommand, Result<ResearchJobDto>>, CancelResearchJobHandler>();

        return services;
    }

    /// <summary>Registers Jobs worker/coordinator handlers, reply gate, lifecycle orchestration, and domain event handlers.</summary>
    public static IServiceCollection AddJobsWorkerApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddJobsApplicationCore();

        services.TryAddScoped<JobReplyGate>();
        services.AddScoped<IRequestHandler<RecoverStuckJobsCommand, int>, RecoverStuckJobsHandler>();
        services.AddScoped<IRequestHandler<EnforceJobDeadlinesCommand, int>, EnforceJobDeadlinesHandler>();

        return services;
    }

    private static void AddJobsApplicationCore(this IServiceCollection services)
    {
        services.TryAddSingleton<JobsMetrics>();
        services.TryAddScoped<StepEmitter>();
        services.TryAddScoped<JobLifecycle>();

        services.AddScoped<IDomainEventHandler<JobCreatedDomainEvent>, JobCreatedDomainEventHandler>();
        services.AddScoped<IDomainEventHandler<JobGatheringStartedDomainEvent>, JobGatheringStartedDomainEventHandler>();
        services.AddScoped<IDomainEventHandler<JobSynthesisStartedDomainEvent>, JobSynthesisStartedDomainEventHandler>();
        services.AddScoped<IDomainEventHandler<JobCompletedDomainEvent>, JobCompletedDomainEventHandler>();
        services.AddScoped<IDomainEventHandler<JobCancelledDomainEvent>, JobCancelledDomainEventHandler>();
        services.AddScoped<IDomainEventHandler<JobFailedDomainEvent>, JobFailedDomainEventHandler>();
        services.AddScoped<IDomainEventHandler<JobExpiredDomainEvent>, JobExpiredDomainEventHandler>();
    }
}
