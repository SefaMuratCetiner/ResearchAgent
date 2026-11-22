namespace Research.Fetching.Application.FetchPage;

/// <summary>A fetched page whose text is already in the document store under <see cref="BlobKey"/>.</summary>
public sealed record FetchedPage(
    string Url,
    string UrlHash,
    string FinalUrl,
    string Title,
    string MimeType,
    long SizeBytes,
    string ContentHash,
    int CharacterCount,
    string BlobKey);
