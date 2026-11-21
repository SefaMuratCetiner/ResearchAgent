namespace Research.Jobs.Application.CancelResearchJob;

/// <summary>Stop a job. Work already in flight may finish and be billed; its result is discarded.</summary>
public sealed record CancelResearchJobCommand(Guid JobId);
