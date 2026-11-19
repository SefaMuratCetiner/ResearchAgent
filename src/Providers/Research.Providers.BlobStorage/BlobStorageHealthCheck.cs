using Azure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Research.Providers.BlobStorage;

/// <summary>
/// Checks the blob service answers. Reported Degraded (§15.5): consumers that need a blob fail and retry on their own,
/// so the replica stays in rotation for the work that does not.
/// </summary>
public sealed class BlobStorageHealthCheck(BlobServiceClient serviceClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await serviceClient.GetPropertiesAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is RequestFailedException or AggregateException or TimeoutException)
        {
            return HealthCheckResult.Degraded("Blob storage is unreachable.", exception);
        }
    }
}
