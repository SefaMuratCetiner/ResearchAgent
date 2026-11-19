using Research.SharedKernel.Errors;
using Research.SharedKernel.Results;

namespace Research.Jobs;

/// <summary>Every error code the Jobs service owns. Documented in docs/errors.md; never renamed or reused.</summary>
public static class JobsErrors
{
    public static readonly Error JobNotFound = new("jobs.job_not_found", ErrorKind.NotFound, "The research job does not exist.");

    public static readonly Error JobAlreadyCompleted = new("jobs.job_already_completed", ErrorKind.Conflict, "The research job has already completed.");

    public static readonly Error JobNotCancellable = new("jobs.job_not_cancellable", ErrorKind.Conflict, "The research job can no longer be cancelled.");

    public static readonly Error ReportNotReady = new("jobs.report_not_ready", ErrorKind.Conflict, "The report is not ready yet.");

    public static readonly Error ReportNotAvailable = new("jobs.report_not_available", ErrorKind.NotFound, "The research job ended without a report.");

    public static readonly Error IdempotencyConflict = new("jobs.idempotency_conflict", ErrorKind.Conflict, "The idempotency key was already used for a different question.");

    public static readonly Error QuestionInvalid = new("jobs.question_invalid", ErrorKind.Invalid, "The question is empty or outside the allowed length.");

    public static readonly Error CapacityReached = new("jobs.capacity_reached", ErrorKind.Unavailable, "Too many research jobs are running; try again later.");

    public static readonly Error PlanEmpty =new("jobs.plan_empty", ErrorKind.Rule, "The plan contained no usable sub-questions.");

    public static readonly Error NoSourcesFound = new("jobs.no_sources_found", ErrorKind.Rule, "No source could be indexed.");

    public static readonly Error InsufficientSources = new("jobs.insufficient_sources", ErrorKind.Rule, "Too few sources were indexed to write a report.");

    public static readonly Error DeadlineExceeded = new("jobs.deadline_exceeded", ErrorKind.Rule, "The job did not finish before its deadline.");

    public static readonly Error BudgetExhausted = new("jobs.budget_exhausted", ErrorKind.Rule, "The job's token budget ran out while gathering.");

    public static readonly Error CutoffReached = new("jobs.cutoff_reached", ErrorKind.Rule, "Gathering was cut short by the synthesis cutoff.");

    public static readonly Error StepsFailed = new("jobs.steps_failed", ErrorKind.Rule, "Some search or fetch steps failed.");

    /// <summary>The warning Synthesis' truncation becomes on the job. Owned by Synthesis; listed here as a known value.</summary>
    public const string OutputTruncatedWarning = "synthesis.output_truncated";
}

/// <summary>Adds <see cref="JobsErrors"/> to the error catalog.</summary>
public sealed class JobsErrorCatalog : IErrorCatalogContributor
{
    public string Prefix => "jobs.";

    public IEnumerable<Error> GetErrors() =>
    [
        JobsErrors.JobNotFound,
        JobsErrors.JobAlreadyCompleted,
        JobsErrors.JobNotCancellable,
        JobsErrors.ReportNotReady,
        JobsErrors.ReportNotAvailable,
        JobsErrors.IdempotencyConflict,
        JobsErrors.QuestionInvalid,
        JobsErrors.CapacityReached,
        JobsErrors.PlanEmpty,
        JobsErrors.NoSourcesFound,
        JobsErrors.InsufficientSources,
        JobsErrors.DeadlineExceeded,
        JobsErrors.BudgetExhausted,
        JobsErrors.CutoffReached,
        JobsErrors.StepsFailed,
    ];
}
