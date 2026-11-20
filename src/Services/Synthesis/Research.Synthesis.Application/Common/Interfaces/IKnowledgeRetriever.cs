namespace Research.Synthesis.Retrieval;

/// <summary>
/// The passages of one job most similar to the queries, best first. Implemented over the Knowledge API; transport
/// failures throw (the pipeline retries).
/// </summary>
public interface IKnowledgeRetriever
{
    Task<IReadOnlyList<RetrievedPassage>> RetrieveAsync(
        Guid jobId,
        IReadOnlyList<string> queries,
        int topK,
        CancellationToken cancellationToken);
}

public sealed record RetrievedPassage(Guid DocumentId, string Url, string Title, string Text, double Score);
