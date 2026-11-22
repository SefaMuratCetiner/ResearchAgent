using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Infrastructure.Persistence;
using Research.Shared.Adapters.Messaging;
using Research.Shared.Messaging;

namespace Research.Jobs.Infrastructure;

/// <summary>DI registrations for the Jobs Infrastructure layer.</summary>
public static class JobsInfrastructureServiceCollectionExtensions
{
    /// <summary>Registers Jobs Postgres repositories and outbox envelope creation.</summary>
    public static IServiceCollection AddJobsInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<EnvelopeFactory>();
        services.TryAddSingleton<IEnvelopeFactory>(static provider => provider.GetRequiredService<EnvelopeFactory>());
        services.TryAddScoped<IResearchJobRepository, ResearchJobRepository>();
        services.TryAddScoped<IJobStepRepository, JobStepRepository>();
        services.TryAddScoped<IResearchReportRepository, ResearchReportRepository>();

        return services;
    }
}
