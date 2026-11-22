using Research.Shared.Kernel.Entities;

namespace Research.Fetching.Domain;

/// <summary>
/// A page fetched for a job. The text lives in the document store under <see cref="BlobKey"/>; the row keeps what is
/// needed to find and deduplicate it. One row per (job, URL).
/// </summary>
public sealed class FetchedDocument : Entity
{
    public required Guid JobId { get; init; }

    /// <summary>The URL as requested (already canonical from Search).</summary>
    public required string Url { get; init; }

    /// <summary>SHA-256 of <see cref="Url"/>.</summary>
    public required string UrlHash { get; init; }

    /// <summary>Where the redirects ended.</summary>
    public required string FinalUrl { get; init; }

    /// <summary>SHA-256 of the extracted text: Knowledge deduplicates on it.</summary>
    public required string ContentHash { get; init; }

    public required string Title { get; init; }

    public required string MimeType { get; init; }

    public required long SizeBytes { get; init; }

    public required int CharacterCount { get; init; }

    public required string BlobKey { get; init; }
}
