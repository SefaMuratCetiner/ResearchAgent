using Research.Jobs.Application.Common.Models;

namespace Research.Jobs.Application.CreateResearchJob;

/// <summary>The job DTO, and whether this call created it (false: an earlier call with the same key did).</summary>
public sealed record CreateResearchJobResult(ResearchJobDto Job, bool Created);
