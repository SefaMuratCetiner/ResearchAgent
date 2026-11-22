using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Research.Shared.Ports.Documents;

namespace Research.Providers.BlobStorage;

/// <summary>DI registrations for the Azure Blob Storage document store provider.</summary>
public static class BlobStorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="BlobDocumentStore"/> as <see cref="IDocumentStore"/> and its Degraded-on-failure health check.
    /// The <c>BlobServiceClient</c> is supplied by the Host's Aspire integration.
    /// </summary>
    public static IServiceCollection AddBlobDocumentStore(this IServiceCollection services, string healthCheckName = "blobs")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(healthCheckName);

        services.TryAddSingleton<IDocumentStore, BlobDocumentStore>();
        services.AddHealthChecks().AddCheck<BlobStorageHealthCheck>(healthCheckName, failureStatus: HealthStatus.Degraded);
        return services;
    }
}
