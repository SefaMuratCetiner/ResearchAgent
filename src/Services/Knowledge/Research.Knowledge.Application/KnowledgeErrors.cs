using Research.Shared.Kernel.Results;

namespace Research.Knowledge.Application;

/// <summary>Every error code the Knowledge service owns.</summary>
public static class KnowledgeErrors
{
    /// <summary>The claim-check blob is gone (it is content-addressed and never deleted, so this is a real fault).</summary>
    public static readonly Error DocumentMissing = new("knowledge.document_missing", ErrorKind.Rule, "The fetched document's text is not in the document store.");

    public static readonly Error NoPassages = new("knowledge.no_passages", ErrorKind.Rule, "The document produced no passages to index.");

    public static readonly Error EmbeddingDimensionMismatch = new("knowledge.embedding_dimension_mismatch", ErrorKind.Unexpected, "The embedding model returned vectors of the wrong size.");

    public static readonly Error InvalidSearch = new("knowledge.invalid_search", ErrorKind.Invalid, "The passage search request is invalid.");

    public static readonly ErrorSet All = new(
        "knowledge.",
        [
            DocumentMissing,
            NoPassages,
            EmbeddingDimensionMismatch,
            InvalidSearch,
        ]);
}
