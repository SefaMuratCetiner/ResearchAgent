using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Research.Fetching.Application.FetchPage;
using Research.Fetching.Application.Observability;

namespace Research.Fetching.Application;

/// <summary>DI registrations for the Fetching Application layer.</summary>
public static class FetchingApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddFetchingApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<FetchingMetrics>();
        services.AddScoped<PageFetchService>();

        return services;
    }
}
