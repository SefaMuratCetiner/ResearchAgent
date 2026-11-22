using Microsoft.Extensions.Logging;
using Research.Shared.Ports.Caching;
using StackExchange.Redis;

namespace Research.Providers.Valkey;

/// <summary>
/// String cache on Valkey. Keys are prefixed with the owning service (<c>planning:</c>, <c>search:</c>) by the caller.
/// A Valkey outage costs only cache hits: reads return a miss and writes are skipped.
/// </summary>
public sealed partial class ValkeyCacheStore(IConnectionMultiplexer multiplexer, ILogger<ValkeyCacheStore> logger) : ICacheStore
{
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var value = await multiplexer.GetDatabase().StringGetAsync(key).WaitAsync(cancellationToken).ConfigureAwait(false);
            return value.IsNullOrEmpty ? null : value.ToString();
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            LogCacheFailed(logger, exception, "read");
            return null;
        }
    }

    public async Task SetAsync(string key, string value, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        try
        {
            await multiplexer.GetDatabase().StringSetAsync(key, value, timeToLive).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            LogCacheFailed(logger, exception, "write");
        }
    }

    [LoggerMessage(EventId = 8400, Level = LogLevel.Warning, Message = "Cache {Operation} failed; continuing without the cache")]
    private static partial void LogCacheFailed(ILogger logger, Exception exception, string operation);
}
