using Research.Providers.AnthropicWebSearch;
using Research.Shared.Adapters.Composition;

namespace Research.Search.Bootstrap.Features;

/// <summary>
/// The search provider: Anthropic web search. It builds its own SDK client from <c>AnthropicWebSearch:ApiKey</c> and
/// rebuilds it when the key is rotated. Swapping engines means swapping this feature.
/// </summary>
internal sealed class AnthropicWebSearchFeature : IResearchFeature
{
    public void Register(ResearchBuilder app) => app.Services.AddAnthropicWebSearch();
}
