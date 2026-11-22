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
