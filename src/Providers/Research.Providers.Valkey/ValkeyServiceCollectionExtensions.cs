using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Research.Shared.Core.Caching;

namespace Research.Providers.Valkey;

/// <summary>DI registrations for the Valkey cache provider.</summary>
public static class ValkeyServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ValkeyCacheStore"/> as <see cref="ICacheStore"/> and its Degraded-on-failure health check.
    /// The <c>IConnectionMultiplexer</c> is supplied by the Host's Aspire integration.
    /// </summary>
    public static IServiceCollection AddValkeyCache(this IServiceCollection services, string healthCheckName = "cache")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(healthCheckName);

        services.TryAddSingleton<ICacheStore, ValkeyCacheStore>();
        services.AddHealthChecks().AddCheck<ValkeyHealthCheck>(healthCheckName, failureStatus: HealthStatus.Degraded);
        return services;
    }
}
