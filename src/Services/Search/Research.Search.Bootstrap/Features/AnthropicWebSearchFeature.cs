using Research.ServiceDefaults.Composition;
using Research.Providers.AnthropicWebSearch;
using Research.Search.Infrastructure.Decorators;
using Research.Shared.Core.Search;

namespace Research.Search.Bootstrap.Features;

/// <summary>
/// The search provider: Anthropic web search. It builds its own SDK client from <c>AnthropicWebSearch:ApiKey</c> and
/// rebuilds it when the key is rotated. Swapping engines means swapping this feature.
/// </summary>
internal sealed class AnthropicWebSearchFeature : IResearchFeature
{
    public void Register(ResearchBuilder app) =>
        app.Services.AddSingleton<ISearchProvider, AnthropicWebSearchProvider>();
}
