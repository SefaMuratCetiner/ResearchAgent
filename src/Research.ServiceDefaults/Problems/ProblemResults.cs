using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Research.SharedKernel.Results;

namespace Research.ServiceDefaults.Problems;

/// <summary>
/// Builds standard ASP.NET Core RFC 9457 <c>ProblemDetails</c> / <c>HttpValidationProblemDetails</c> responses
/// from domain and application <see cref="Error"/> values.
/// </summary>
public static class ProblemResults
{
    /// <summary>The documentation base every problem <c>type</c> URI points into.</summary>
    public const string TypeBase = "https://docs.research.local/errors/";

    public static IResult From(Error error, HttpContext context, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(context);

        return TypedResults.Problem(
            detail: detail,
            instance: context.Request.Path.Value,
            statusCode: ErrorStatus.From(error.Kind),
            title: error.Title,
            type: TypeBase + error.Code,
            extensions: Extensions(error.Code, context));
    }

    public static IResult Validation(IReadOnlyDictionary<string, string[]> errors, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(context);

        var error = ProblemErrors.ValidationFailed;
        return TypedResults.ValidationProblem(
            errors: errors,
            detail: "One or more fields are not valid.",
            instance: context.Request.Path.Value,
            title: error.Title,
            type: TypeBase + error.Code,
            extensions: Extensions(error.Code, context));
    }

    private static Dictionary<string, object?> Extensions(string code, HttpContext context) => new(StringComparer.Ordinal)
    {
        ["code"] = code,
        ["traceId"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
    };
}
