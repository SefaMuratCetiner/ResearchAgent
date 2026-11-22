namespace Research.Shared.Ports.Search;

/// <summary>Hits in the provider's ranking order, and the tokens the search cost (LLM-based providers).</summary>
public sealed record SearchResponse(IReadOnlyList<SearchHit> Hits, long InputTokens, long OutputTokens);
