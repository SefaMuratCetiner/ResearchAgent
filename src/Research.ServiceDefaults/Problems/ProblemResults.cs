using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Research.SharedKernel.Results;

namespace Research.ServiceDefaults.Problems;

/// <summary>Builds problem responses from errors. Endpoints return <see cref="From"/> for every failed <c>Result</c>.</summary>
public static class ProblemResults
{
    public static IResult From(Error error, HttpContext context, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(context);
        var problem = Create(error, context, detail, errors: null);
        return Results.Json(problem, statusCode: problem.Status, contentType: ResearchProblem.ContentType);
    }

    public static IResult Validation(IReadOnlyDictionary<string, string[]> errors, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var problem = Create(ProblemErrors.ValidationFailed, context, "One or more fields are not valid.", errors);
        return Results.Json(problem, statusCode: problem.Status, contentType: ResearchProblem.ContentType);
    }

    public static ResearchProblem Create(
        Error error,
        HttpContext context,
        string? detail,
        IReadOnlyDictionary<string, string[]>? errors)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(context);
        return new ResearchProblem(
            ResearchProblem.TypeBase + error.Code,
            error.Title,
            ErrorStatus.From(error.Kind),
            detail,
            context.Request.Path.Value,
            error.Code,
            Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
            errors);
    }
}
