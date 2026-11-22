using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Research.Knowledge.Application.Common.Interfaces;
using Research.Knowledge.Infrastructure.Persistence;

namespace Research.Knowledge.Infrastructure;

/// <summary>DI registrations for the Knowledge Infrastructure layer.</summary>
public static class KnowledgeInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddKnowledgeInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IKnowledgeRepository, KnowledgeRepository>();

        return services;
    }
}
