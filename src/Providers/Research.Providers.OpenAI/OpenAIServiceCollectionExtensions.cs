using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Research.Shared.Core.Llm;
using Research.Shared.Core.Llm.Observability;

namespace Research.Providers.OpenAI;

/// <summary>DI registrations for the OpenAI LLM provider.</summary>
public static class OpenAIServiceCollectionExtensions
{
    /// <summary>
    /// Registers the OpenAI SDK client source, shared LLM metrics, and the concurrency-limited
    /// <see cref="ILlmClient"/>. Options (<see cref="OpenAIOptions"/>) are bound by the Host.
    /// </summary>
    public static IServiceCollection AddOpenAILlm(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<OpenAIClientProvider>();
        services.TryAddSingleton<LlmMetrics>();
        services.TryAddSingleton<OpenAILlmClient>();
        services.AddSingleton<ILlmClient>(static provider =>
        {
            var options = provider.GetRequiredService<IOptionsMonitor<OpenAIOptions>>();
            return new RateLimitedLlmClient(
                provider.GetRequiredService<OpenAILlmClient>(),
                () => options.CurrentValue.MaxConcurrentRequests);
        });

        return services;
    }
}
