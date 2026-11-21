using Research.Shared.Core.Errors;
using Research.SharedKernel.Results;

namespace Research.Planning.Application;

/// <summary>Every error code the Planning service owns. They reach Jobs as <c>StepFailed.ErrorCode</c>.</summary>
public static class PlanningErrors
{
    public static readonly Error InvalidPlan = new("planning.invalid_plan", ErrorKind.Rule, "The model did not return a usable plan.");

    public static readonly Error LlmRefused = new("planning.llm_refused", ErrorKind.Rule, "The model refused to plan this question.");
}

/// <summary>Adds <see cref="PlanningErrors"/> to the error catalog.</summary>
public sealed class PlanningErrorCatalog : IErrorCatalogContributor
{
    public string Prefix => "planning.";

    public IEnumerable<Error> GetErrors() => [PlanningErrors.InvalidPlan, PlanningErrors.LlmRefused];
}
