using Research.Knowledge.Domain;

namespace Research.Knowledge.Application.IndexDocument;

/// <summary>
/// What indexing needs to write: either the id of a document with the same content that is already indexed, or a new
/// document with its embedded passages.
/// </summary>
public sealed record PreparedDocument(StoredDocument? Existing, KnowledgeDocument? NewDocument, IReadOnlyList<Passage> Passages)
{
    public static PreparedDocument From(StoredDocument existing) => new(existing, null, []);
}
