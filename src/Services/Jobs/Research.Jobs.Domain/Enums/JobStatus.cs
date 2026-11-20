namespace Research.Jobs.Domain;

/// <summary>The states of a research job (§10.1). Terminal states are absorbing.</summary>
public enum JobStatus
{
    Planning,
    Gathering,
    Synthesizing,
    Completed,
    CompletedWithWarnings,
    Failed,
    Cancelled,
    Expired,
}

/// <summary>Why gathering ended.</summary>
public enum GatheringEndReason
{
    /// <summary>Every search and fetch step reached a terminal state.</summary>
    AllStepsDone,

    /// <summary><c>synthesis_cutoff_at</c> passed first.</summary>
    CutoffReached,

    /// <summary>The token budget ran out first.</summary>
    BudgetExhausted,
}

public static class JobStatusExtensions
{
    public static bool IsTerminal(this JobStatus status) => status >= JobStatus.Completed;

    public static bool HasReport(this JobStatus status) =>
        status is JobStatus.Completed or JobStatus.CompletedWithWarnings;
}
