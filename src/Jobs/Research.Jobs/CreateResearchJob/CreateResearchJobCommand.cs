using Research.Jobs.Domain;

namespace Research.Jobs.CreateResearchJob;

/// <summary>Start researching <see cref="Question"/>. A repeated <see cref="IdempotencyKey"/> returns the first job.</summary>
public sealed record CreateResearchJobCommand(string Question, string? IdempotencyKey);

/// <summary>The job, and whether this call created it (false: an earlier call with the same key did).</summary>
public sealed record CreateResearchJobResult(ResearchJob Job, bool Created);
