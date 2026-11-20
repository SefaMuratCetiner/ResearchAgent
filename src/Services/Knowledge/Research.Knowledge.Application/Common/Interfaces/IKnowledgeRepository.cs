using Research.Knowledge.Domain;
using Research.Shared.Core.Transactions;

namespace Research.Knowledge.Application.Common.Interfaces;

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
