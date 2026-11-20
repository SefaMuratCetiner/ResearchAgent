using Microsoft.Extensions.Options;

namespace Research.Knowledge;

/// <summary>Bound from the <c>Knowledge</c> section.</summary>
public sealed class KnowledgeOptions
{
    public const string SectionName = "Knowledge";

    /// <summary>Target passage size in characters.</summary>
    public int ChunkChars { get; set; } = 1200;

    /// <summary>Characters repeated from the end of one passage at the start of the next.</summary>
    public int ChunkOverlapChars { get; set; } = 200;

    /// <summary>Passages kept per document; the rest of a very long page is not indexed.</summary>
    public int MaxPassagesPerDocument { get; set; } = 200;

    /// <summary>Texts per embedding call.</summary>
    public int EmbeddingBatchSize { get; set; } = 64;

    /// <summary>Upper bound for <c>topK</c> in a passage search.</summary>
    public int MaxTopK { get; set; } = 50;

    /// <summary>Upper bound for the number of queries in one passage search.</summary>
    public int MaxQueries { get; set; } = 20;

    public int MaxQueryLength { get; set; } = 2000;
}

/// <summary>Checks <see cref="KnowledgeOptions"/> at startup.</summary>
public sealed class KnowledgeOptionsValidator : IValidateOptions<KnowledgeOptions>
{
    public ValidateOptionsResult Validate(string? name, KnowledgeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (options.ChunkChars < 200)
        {
            failures.Add("Knowledge:ChunkChars must be at least 200.");
        }

        if (options.ChunkOverlapChars < 0 || options.ChunkOverlapChars >= options.ChunkChars / 2)
        {
            failures.Add("Knowledge:ChunkOverlapChars must be non-negative and below half of ChunkChars.");
        }

        if (options.MaxPassagesPerDocument < 1)
        {
            failures.Add("Knowledge:MaxPassagesPerDocument must be at least 1.");
        }

        if (options.EmbeddingBatchSize < 1)
        {
            failures.Add("Knowledge:EmbeddingBatchSize must be at least 1.");
        }

        if (options.MaxTopK < 1 || options.MaxQueries < 1 || options.MaxQueryLength < 1)
        {
            failures.Add("Knowledge:MaxTopK, MaxQueries and MaxQueryLength must be at least 1.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
