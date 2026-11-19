using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Research.Fetching.Download;

/// <summary>
/// Limits concurrent downloads per host on this replica, so one job never hammers one site. The limit reloads: when
/// <see cref="FetchingOptions.PerDomainConcurrency"/> changes, the next download to a host gets a gate of the new
/// size. Downloads already holding the old gate release it as usual.
/// </summary>
public sealed class DomainThrottle(IOptionsMonitor<FetchingOptions> options) : IDisposable
{
    private readonly ConcurrentDictionary<string, Gate> _hosts = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IDisposable> AcquireAsync(string host, CancellationToken cancellationToken)
    {
        var limit = options.CurrentValue.PerDomainConcurrency;
        var gate = _hosts.AddOrUpdate(
            host,
            static (_, size) => new Gate(size),
            static (_, existing, size) => existing.Limit == size ? existing : new Gate(size),
            limit);
        await gate.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Release(gate.Semaphore);
    }

    public void Dispose()
    {
        foreach (var gate in _hosts.Values)
        {
            gate.Semaphore.Dispose();
        }
    }

    // A replaced gate is not disposed: holders still release it. SemaphoreSlim needs disposing only when its wait
    // handle was used, which it never is here, so dropping it is safe.
    private sealed record Gate(int Limit)
    {
        public SemaphoreSlim Semaphore { get; } = new(Limit, Limit);
    }

    private sealed class Release(SemaphoreSlim gate) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                gate.Release();
            }
        }
    }
}
