using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Research.Jobs.Api.Common.Models;
using Research.Jobs.Application.CreateResearchJob;
using Research.Shared.Core.Mediation;
using Research.Shared.Web.Endpoints;
using Research.Shared.Web.Problems;

namespace Research.Jobs.Api.CreateResearchJob;

/// <summary>
/// <c>POST /research</c>: accepts a question and returns <c>202</c> with the job and its links. A repeated
/// <c>Idempotency-Key</c> with the same question returns the first job; with another question, <c>409</c>.
/// </summary>
public sealed class CreateResearchJobEndpoint : IEndpoint
{
    public const string Pattern = "/";
    public const string Name = "CreateResearchJob";
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(Pattern, HandleAsync)
            .WithName(Name)
            .WithSummary("Start a research job")
            .WithRequestValidation<CreateResearchJobRequest, CreateResearchJobRequestValidator>()
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
}
