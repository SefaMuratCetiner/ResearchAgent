using Research.SharedKernel.Entities;

namespace Research.Knowledge.Domain;

/// <summary>
/// One indexed text, stored once per (content hash, embedding model) and shared by every job that fetched the same
/// text. Which job uses it, and under which URL, is a <see cref="JobDocumentLink"/>.
/// </summary>
public sealed class KnowledgeDocument : Entity
{
    public required string ContentHash { get; init; }

    public required string EmbeddingModel { get; init; }

    /// <summary>The URL it was first indexed from.</summary>
    public required string Url { get; init; }

    public required string Title { get; init; }

    public required int CharacterCount { get; init; }

    public required int PassageCount { get; init; }
}

/// <summary>An indexed document found by content hash.</summary>
public sealed record StoredDocument(Guid Id, int PassageCount);

/// <summary>A chunk of a document and its embedding.</summary>
public sealed record Passage(Guid Id, int Ordinal, string Text, float[] Embedding);

/// <summary>A job uses a document, found at <see cref="Url"/>.</summary>
public sealed record JobDocumentLink(
    Guid JobId,
    Guid DocumentId,
    string Url,
    string Title,
    DateTimeOffset CreatedAt = default,
    string CreatedBy = "");

/// <summary>A passage found by a similarity search; <see cref="Score"/> is cosine similarity (1 = identical).</summary>
public sealed record PassageMatch(Guid PassageId, Guid DocumentId, string Url, string Title, string Text, double Score);
