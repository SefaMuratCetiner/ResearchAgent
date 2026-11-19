namespace Research.SharedKernel.Caching;

/// <summary>
/// A best-effort string cache. The cache is an optimization, never the system of record: implementations swallow and
/// log their own transport failures, returning a miss on read and doing nothing on write.
/// </summary>
public interface ICacheStore
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    Task SetAsync(string key, string value, TimeSpan timeToLive, CancellationToken cancellationToken);
}
