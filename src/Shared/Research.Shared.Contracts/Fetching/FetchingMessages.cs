namespace Research.Shared.Contracts.Fetching;

/// <summary>Jobs asks Fetching to download and extract one page.</summary>
public sealed record FetchRequested(Guid JobId, string StepKey, string Url, string? Title) : IIntegrationMessage
{
    public static string MessageType => "fetching.fetch-requested";

    public static MessageKind Kind => MessageKind.Command;
}

/// <summary>
/// The page's text is in the document store under <see cref="BlobKey"/> (claim check). The message never carries the
/// text itself.
/// </summary>
public sealed record DocumentFetched(
    Guid JobId,
    string StepKey,
    Guid FetchedDocumentId,
    string Url,
    string Title,
    string BlobKey,
    string ContentHash,
    int CharacterCount) : IIntegrationMessage
{
    public static string MessageType => "fetching.document-fetched";

    public static MessageKind Kind => MessageKind.Event;
}
