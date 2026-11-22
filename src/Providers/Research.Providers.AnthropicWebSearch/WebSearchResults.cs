using Research.Shared.Ports.Search;

namespace Research.Providers.AnthropicWebSearch;

/// <summary>Hits in order of appearance, and the error codes of failed searches.</summary>
public sealed record WebSearchResults(IReadOnlyList<SearchHit> Hits, IReadOnlyList<string> ErrorCodes);
