namespace Research.SharedKernel.Concurrency;

/// <summary>
/// A concurrency limit whose size can change while the process runs, so a limit read from reloading options takes
/// effect without a restart. <paramref name="limit"/> is read on every acquire; when it differs from the current
/// gate's size, a new gate of the new size takes over for later callers. Callers holding the old gate release it as
/// usual, so for a short time both gates may admit work: the change is eventual, not exact.
/// </summary>
public sealed class ResizableConcurrencyLimit(Func<int> limit) : IDisposable
{
    private Gate? _gate;

    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        var gate = Current();
        await gate.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Release(gate.Semaphore);
    }

    public void Dispose() => Volatile.Read(ref _gate)?.Semaphore.Dispose();

    private Gate Current()
    {
        var size = Math.Max(1, limit());
        while (true)
        {
            var gate = Volatile.Read(ref _gate);
            if (gate is not null && gate.Limit == size)
            {
                return gate;
            }

            // A replaced gate is not disposed: holders still release it. SemaphoreSlim needs disposing only when its
            // wait handle was used, which it never is here.
            var replacement = new Gate(size);
            if (Interlocked.CompareExchange(ref _gate, replacement, gate) == gate)
            {
                return replacement;
            }
        }
    }

    private sealed record Gate(int Limit)
    {
        public SemaphoreSlim Semaphore { get; } = new(Limit, Limit);
    }

    private sealed class Release(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                semaphore.Release();
            }
        }
    }
}
