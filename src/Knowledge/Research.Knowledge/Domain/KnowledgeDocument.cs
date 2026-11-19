using Research.SharedKernel.Entities;
using Research.SharedKernel.Transactions;

namespace Research.Knowledge.Domain;

/// <summary>
/// One indexed text, stored once per (content hash, embedding model) and shared by every job that fetched the same
/// text. Which job uses it, and under which URL, is a <see cref="JobDocumentLink"/>.
/// </summary>
public sealed record KnowledgeDocument : AuditableEntity
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
public sealed record JobDocumentLink(Guid JobId, Guid DocumentId, string Url, string Title, DateTimeOffset CreatedAt, string CreatedBy);

/// <summary>A passage found by a similarity search; <see cref="Score"/> is cosine similarity (1 = identical).</summary>
public sealed record PassageMatch(Guid PassageId, Guid DocumentId, string Url, string Title, string Text, double Score);

/// <summary>Persistence of documents, passages and job links.</summary>
public interface IKnowledgeRepository
{
    /// <returns>The document with this content hash and model, or null.</returns>
    Task<StoredDocument?> FindDocumentAsync(string contentHash, string embeddingModel, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts the document and its passages unless another replica stored the same content first.
    /// </summary>
    /// <returns>The stored document and whether this call inserted it.</returns>
    Task<(StoredDocument Document, bool Inserted)> InsertDocumentAsync(
        KnowledgeDocument document,
        IReadOnlyList<Passage> passages,
        ITransactionScope transaction,
        CancellationToken cancellationToken);

    /// <summary>Links the document to the job; an existing link is kept.</summary>
    Task LinkAsync(JobDocumentLink link, ITransactionScope transaction, CancellationToken cancellationToken);

    /// <summary>The <paramref name="topK"/> passages of the job's documents closest to <paramref name="embedding"/>.</summary>
    Task<IReadOnlyList<PassageMatch>> SearchAsync(
        Guid jobId,
        float[] embedding,
        string embeddingModel,
        int topK,
        CancellationToken cancellationToken);
}
