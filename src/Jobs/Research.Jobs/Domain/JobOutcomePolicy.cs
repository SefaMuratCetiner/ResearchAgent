using Research.SharedKernel.Results;

namespace Research.Jobs.Domain;

/// <summary>The decisions of §10.1: whether gathering can lead to synthesis, and the final state after the report.</summary>
public static class JobOutcomePolicy
{
    /// <summary>The error the job fails with when gathering ended, or null when it may synthesize.</summary>
    public static Error? EvaluateSources(int indexedSources, int minSources) => indexedSources switch
    {
        0 => JobsErrors.NoSourcesFound,
        _ when indexedSources < minSources => JobsErrors.InsufficientSources,
        _ => null,
    };

    /// <summary>The terminal state and warnings once the report is stored.</summary>
    public static (JobStatus Status, IReadOnlyList<string> Warnings) Complete(
        GatheringEndReason? gatheringEndReason,
        int failedSteps,
        bool reportTruncated)
    {
        var warnings = new List<string>();
        if (gatheringEndReason == GatheringEndReason.CutoffReached)
        {
            warnings.Add(JobsErrors.CutoffReached.Code);
        }

        if (gatheringEndReason == GatheringEndReason.BudgetExhausted)
        {
            warnings.Add(JobsErrors.BudgetExhausted.Code);
        }

        if (failedSteps > 0)
        {
            warnings.Add(JobsErrors.StepsFailed.Code);
        }

        if (reportTruncated)
        {
            warnings.Add(JobsErrors.OutputTruncatedWarning);
        }

        return (warnings.Count == 0 ? JobStatus.Completed : JobStatus.CompletedWithWarnings, warnings);
    }
}
