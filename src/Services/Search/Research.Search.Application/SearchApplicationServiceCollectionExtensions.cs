using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Research.Search.Application.RunSearch;
using Research.Shared.Core.Mediation;
using Research.Shared.Ports.Search;
using Research.SharedKernel.Results;

namespace Research.Search.Application;

/// <summary>DI registrations for the Search Application layer.</summary>
public static class SearchApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddSearchApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<SearchMetrics>();
        services.AddScoped<IRequestHandler<RunSearchCommand, Result<SearchResponse>>, RunSearchHandler>();

        return services;
    }
}
