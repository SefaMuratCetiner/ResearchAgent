using Research.SharedKernel.Entities;

namespace Research.Jobs.Domain;

/// <summary>
/// The research job aggregate. Every change is a new copy (<c>with</c>) saved by a compare-and-set update on
/// <see cref="Version"/> and the allowed source states (<see cref="JobStateMachine"/>).
/// </summary>
public sealed record ResearchJob : AuditableEntity
{
    public required string Question { get; init; }

    public required JobStatus Status { get; init; }

    /// <summary>The sub-questions from Planning; empty until the plan arrives.</summary>
    public IReadOnlyList<string> Plan { get; init; } = [];

    public string? IdempotencyKey { get; init; }

    /// <summary>When gathering ends regardless of open steps.</summary>
    public required DateTimeOffset SynthesisCutoffAt { get; init; }

    /// <summary>When an active job expires.</summary>
    public required DateTimeOffset DeadlineAt { get; init; }

    public required long TokenBudget { get; init; }

    public long TokensUsed { get; init; }

    /// <summary>Set on <see cref="JobStatus.Failed"/> and <see cref="JobStatus.Expired"/>.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Warning codes of <see cref="JobStatus.CompletedWithWarnings"/>.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public GatheringEndReason? GatheringEndReason { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public int Version { get; init; }

    public bool BudgetExhausted => TokensUsed >= TokenBudget;
}
