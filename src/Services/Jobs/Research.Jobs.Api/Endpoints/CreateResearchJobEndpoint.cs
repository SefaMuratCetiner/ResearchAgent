using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Research.Jobs.Application.CreateResearchJob;
using Research.Shared.Core.Mediation;
using Research.Shared.Web.Endpoints;
using Research.Shared.Web.Problems;

namespace Research.Jobs.Api.Endpoints;

/// <summary>Body of <c>POST /research</c>.</summary>
public sealed record CreateResearchJobRequest(string? Question);

/// <summary>
/// <c>POST /research</c>: accepts a question and returns <c>202</c> with the job and its links. A repeated
/// <c>Idempotency-Key</c> with the same question returns the first job; with another question, <c>409</c>.
/// </summary>
public sealed class CreateResearchJobEndpoint : IEndpoint
{
    public const string Pattern = "/";
    public const string Name = "CreateResearchJob";
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    private const int MaxIdempotencyKeyLength = 200;

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(Pattern, HandleAsync)
            .WithName(Name)
            .WithSummary("Start a research job")
            .WithRequestValidation<CreateResearchJobRequest, Validator>()
            .Accepts<CreateResearchJobRequest>("application/json")
            .Produces<ResearchJobResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

    public static async Task<IResult> HandleAsync(
        CreateResearchJobRequest? request,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        ISender sender,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(context);

        var result = await sender.SendAsync(new CreateResearchJobCommand(request!.Question!, idempotencyKey), cancellationToken)
            .ConfigureAwait(false);

        return result.ToHttpResult(context, value =>
        {
            var response = ResearchJobResponse.From(value.Job);
            return TypedResults.Accepted(response.Links.Self, response);
        });
    }

    /// <summary>Stage-1 HTTP validation: checks required body field and <c>Idempotency-Key</c> header syntax before the endpoint runs.</summary>
    public sealed class Validator : IEndpointValidator<CreateResearchJobRequest>
    {
        public void Validate(CreateResearchJobRequest? request, HttpContext httpContext, IDictionary<string, string[]> errors)
        {
            if (string.IsNullOrWhiteSpace(request?.Question))
            {
                errors["question"] = ["The question is required."];
            }

            if (httpContext.Request.Headers.TryGetValue(IdempotencyKeyHeader, out var values))
            {
                var key = values.ToString();
                if (key.Length is 0 or > MaxIdempotencyKeyLength || key.Any(static c => c is < '!' or > '~'))
                {
                    errors[IdempotencyKeyHeader] = [$"Use 1 to {MaxIdempotencyKeyLength} visible ASCII characters."];
                }
            }
        }
    }
}
