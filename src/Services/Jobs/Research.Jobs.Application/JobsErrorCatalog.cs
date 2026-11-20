using Research.Jobs.Domain;
using Research.Shared.Core.Errors;
using Research.SharedKernel.Results;

namespace Research.Jobs;

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
