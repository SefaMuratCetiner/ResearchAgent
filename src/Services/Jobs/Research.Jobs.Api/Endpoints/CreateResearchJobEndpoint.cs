using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Research.Jobs.CreateResearchJob;
using Research.ServiceDefaults.Middleware;
using Research.ServiceDefaults.Problems;

namespace Research.Jobs.Api.Endpoints;

/// <summary>
/// <c>POST /research</c>: accepts a question and returns <c>202</c> with the job and its links. A repeated
/// <c>Idempotency-Key</c> with the same question returns the first job; with another question, <c>409</c>.
/// </summary>
public static class CreateResearchJobEndpoint
{
    public const string Pattern = "/";
    public const string Name = "CreateResearchJob";
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    private const int MaxIdempotencyKeyLength = 200;

    public static async Task<IResult> HandleAsync(
        CreateResearchJobRequest? request,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        CreateResearchJobHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(context);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(request?.Question))
        {
            errors["question"] = ["The question is required."];
        }

        if (idempotencyKey is not null
            && (idempotencyKey.Length is 0 or > MaxIdempotencyKeyLength || idempotencyKey.Any(static c => c is < '!' or > '~')))
        {
            errors[IdempotencyKeyHeader] = [$"Use 1 to {MaxIdempotencyKeyLength} visible ASCII characters."];
        }

        if (errors.Count > 0)
        {
            return ProblemResults.Validation(errors, context);
        }

        var result = await handler.HandleAsync(new CreateResearchJobCommand(request!.Question!, idempotencyKey), cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return ProblemResults.From(result.Error, context);
        }

        var response = ResearchJobResponse.From(result.Value.Job);
        return TypedResults.Accepted(response.Links.Self, response);
    }
}
