using System.Security.Cryptography;
using System.Text;
using Research.SharedKernel.Entities;

namespace Research.Jobs.Domain;

/// <summary>What a step asks of which service.</summary>
public enum StepKind
{
    Plan,
    Search,

    /// <summary>Fetch and index one page: Fetching downloads it, Knowledge indexes it and answers for the step.</summary>
    Fetch,
    Synthesize,
}

public enum StepStatus
{
    Running,
    Succeeded,
    Failed,
}

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

/// <summary>Step keys: deterministic, so duplicates collapse on the (job id, step key) unique key.</summary>
public static class JobStepKeys
{
    public const string Plan = "plan";
    public const string Synthesize = "synthesize";

    public static string Search(int index) => "search:" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>One fetch step per canonical URL per job: the same URL from two sub-questions is fetched once.</summary>
    public static string Fetch(string canonicalUrl)
    {
        ArgumentNullException.ThrowIfNull(canonicalUrl);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalUrl));
        return "fetch:" + Convert.ToHexStringLower(hash.AsSpan(0, 12));
    }

    public static StepKind KindOf(string stepKey)
    {
        ArgumentNullException.ThrowIfNull(stepKey);
        if (stepKey == Plan)
        {
            return StepKind.Plan;
        }

        if (stepKey == Synthesize)
        {
            return StepKind.Synthesize;
        }

        return stepKey.StartsWith("search:", StringComparison.Ordinal) ? StepKind.Search : StepKind.Fetch;
    }
}
