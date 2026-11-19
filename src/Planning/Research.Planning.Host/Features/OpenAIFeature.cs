using Microsoft.Extensions.Options;
using Research.Hosting.Composition;
using Research.Providers.OpenAI;
using Research.SharedKernel.Llm;

namespace Research.Planning.Host.Features;

/// <summary>
/// The LLM when <c>Llm:Provider</c> is OpenAI: the OpenAI client source (rebuilds the client when the API key is
/// rotated; the key is never logged), wrapped by the per-replica concurrency limiter, which reads
/// OpenAI:MaxConcurrentRequests on every request.
/// </summary>
internal sealed class OpenAIFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Services.AddSingleton<OpenAIClientSource>();
        app.Services.AddSingleton<LlmMetrics>();
        app.Services.AddSingleton<OpenAILlmClient>();
        app.Services.AddSingleton<ILlmClient>(static provider =>
        {
            var options = provider.GetRequiredService<IOptionsMonitor<OpenAIOptions>>();
            return new RateLimitedLlmClient(
                provider.GetRequiredService<OpenAILlmClient>(),
                () => options.CurrentValue.MaxConcurrentRequests);
        });
    }
}
