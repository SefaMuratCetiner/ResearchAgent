using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Research.Shared.Adapters.Llm;
using Research.Shared.Ports.Llm;
using Research.Shared.Adapters.Llm.Observability;

namespace Research.Providers.Anthropic;

/// <summary>DI registrations for the Anthropic LLM provider.</summary>
public static class AnthropicServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Anthropic SDK client source, shared LLM metrics, and the concurrency-limited
    /// <see cref="ILlmClient"/>. Options (<see cref="AnthropicOptions"/>) are bound by the Host.
    /// </summary>
    public static IServiceCollection AddAnthropicLlm(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<AnthropicClientSource>();
        services.TryAddSingleton<LlmMetrics>();
        services.TryAddSingleton<AnthropicLlmClient>();
        services.AddSingleton<ILlmClient>(static provider =>
        {
            var options = provider.GetRequiredService<IOptionsMonitor<AnthropicOptions>>();
            return new RateLimitedLlmClient(
                provider.GetRequiredService<AnthropicLlmClient>(),
                () => options.CurrentValue.MaxConcurrentRequests);
        });

        return services;
    }
}
