using Research.SharedKernel.Entities;

namespace Research.Jobs.Domain;

/// <summary>
/// One request Jobs is owed an answer for. The request itself is stored (<see cref="RequestType"/>,
/// <see cref="RequestPayload"/>) so recovery can re-emit it when the lease expires without an answer.
/// </summary>
public sealed class JobStep : Entity
{
    public required Guid JobId { get; init; }

    public required StepKind Kind { get; init; }

    /// <summary>Unique per job; carried by the request and every reply (<see cref="JobStepKeys"/>).</summary>
    public required string StepKey { get; init; }

    public required StepStatus Status { get; init; }

    /// <summary>How often the request was emitted (first emission included).</summary>
    public int Attempts { get; init; } = 1;

    public required DateTimeOffset LeaseUntil { get; init; }

    public string? LastErrorCode { get; init; }

    /// <summary>The trace the step was emitted in, for jumping from a job to its traces.</summary>
    public string? TraceId { get; init; }

    public required string RequestType { get; init; }

    public required string RequestPayload { get; init; }
}
