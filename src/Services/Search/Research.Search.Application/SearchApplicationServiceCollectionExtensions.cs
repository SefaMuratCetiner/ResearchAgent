using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Research.Search.Application.Observability;
using Research.Search.Application.RunSearch;

namespace Research.Search.Application;

/// <summary>DI registrations for the Search Application layer.</summary>
public static class SearchApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddSearchApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<SearchMetrics>();
        services.AddScoped<WebSearchService>();

        return services;
    }
}
