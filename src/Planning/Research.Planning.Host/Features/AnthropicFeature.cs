using Microsoft.Extensions.Options;
using Research.Hosting.Composition;
using Research.Providers.Anthropic;
using Research.SharedKernel.Llm;

namespace Research.Planning.Host.Features;

/// <summary>
/// The LLM when <c>Llm:Provider</c> is Anthropic (the default): the Anthropic client source (rebuilds the client when
/// the API key is rotated; the key is never logged), wrapped by the per-replica concurrency limiter, which reads
/// Anthropic:MaxConcurrentRequests on every request.
/// </summary>
internal sealed class AnthropicFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Services.AddSingleton<AnthropicClientSource>();
        app.Services.AddSingleton<LlmMetrics>();
        app.Services.AddSingleton<AnthropicLlmClient>();
        app.Services.AddSingleton<ILlmClient>(static provider =>
        {
            var options = provider.GetRequiredService<IOptionsMonitor<AnthropicOptions>>();
            return new RateLimitedLlmClient(
                provider.GetRequiredService<AnthropicLlmClient>(),
                () => options.CurrentValue.MaxConcurrentRequests);
        });
    }
}
