using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Research.Providers.Valkey;

/// <summary>Pings Valkey. The cache is optional, so an outage reports Degraded and the replica stays in rotation.</summary>
public sealed class ValkeyHealthCheck(IConnectionMultiplexer multiplexer) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await multiplexer.GetDatabase().PingAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            return HealthCheckResult.Degraded("Valkey is unreachable; running without the cache.", exception);
        }
    }
}
