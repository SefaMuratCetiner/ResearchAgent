using Research.Providers.Anthropic;
using Research.Shared.Adapters.Composition;

namespace Research.Planning.Bootstrap.Features;

/// <summary>
/// The LLM when <c>Llm:Provider</c> is Anthropic (the default): the Anthropic client source (rebuilds the client when
/// the API key is rotated; the key is never logged), wrapped by the per-replica concurrency limiter, which reads
/// Anthropic:MaxConcurrentRequests on every request.
/// </summary>
internal sealed class AnthropicFeature : IHostFeature
{
    public void Register(ServiceHostBuilder app) => app.Services.AddAnthropicLlm();
}
