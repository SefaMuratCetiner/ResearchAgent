namespace Research.Contracts.Search;

/// <summary>Jobs asks Search for sources for one sub-question.</summary>
public sealed record SearchRequested(Guid JobId, string StepKey, string Query, int MaxResults) : IIntegrationMessage
{
    public static string MessageType => "search.search-requested";

    public static MessageKind Kind => MessageKind.Command;
}

/// <summary>Search's answer. An empty <see cref="Hits"/> list is a valid answer, not a failure.</summary>
public sealed record ResultsFound(
    Guid JobId,
    string StepKey,
    IReadOnlyList<SearchResultItem> Hits,
    long InputTokens,
    long OutputTokens) : IIntegrationMessage
{
    public static string MessageType => "search.results-found";

    public static MessageKind Kind => MessageKind.Event;
}

/// <summary>One search hit. <see cref="Url"/> is canonical (lower-case host, no fragment, no tracking parameters).</summary>
public sealed record SearchResultItem(string Url, string Title, string? PageAge);
