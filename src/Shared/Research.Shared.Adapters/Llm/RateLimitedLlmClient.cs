using Research.Shared.Ports.Llm;
using Research.Shared.Adapters.Concurrency;

namespace Research.Shared.Adapters.Llm;

/// <summary>
/// Caps concurrent LLM requests per replica (D: concurrency limiter only). Requests beyond the cap wait; the SDK's
/// own retries honour <c>retry-after</c> on 429s. The cap is read on every request, so a reloaded
/// <c>MaxConcurrentRequests</c> of the selected provider (<c>Anthropic</c> or <c>OpenAI</c>) applies without a restart.
/// </summary>
public sealed class RateLimitedLlmClient(ILlmClient inner, Func<int> maxConcurrentRequests) : ILlmClient, IDisposable
{
    private readonly ResizableConcurrencyLimit _slots = new(maxConcurrentRequests);

    public async Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        using (await _slots.AcquireAsync(cancellationToken).ConfigureAwait(false))
        {
            return await inner.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose() => _slots.Dispose();
}
