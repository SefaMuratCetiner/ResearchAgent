using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Research.Shared.Ports.Search;

namespace Research.Providers.AnthropicWebSearch;

/// <summary>DI registrations for the Anthropic web search provider.</summary>
public static class AnthropicWebSearchServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AnthropicWebSearchProvider"/> as <see cref="ISearchProvider"/>.
    /// Options (<see cref="AnthropicWebSearchOptions"/>) are bound by the Host.
    /// </summary>
    public static IServiceCollection AddAnthropicWebSearch(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<AnthropicWebSearchClientProvider>();
        services.TryAddSingleton<ISearchProvider, AnthropicWebSearchProvider>();
        return services;
    }
}
