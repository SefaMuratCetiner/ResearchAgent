using Microsoft.AspNetCore.Http;
using Research.Jobs.GetResearchJob;
using Research.Problems;

namespace Research.Jobs.Api.Endpoints;

/// <summary><c>GET /research/{id}</c>: the job, its plan, warnings and step progress.</summary>
public static class GetResearchJobEndpoint
{
    public const string Pattern = "/{id:guid}";
    public const string Name = "GetResearchJob";

    public static async Task<IResult> HandleAsync(Guid id, GetResearchJobHandler handler, HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var result = await handler.HandleAsync(new GetResearchJobQuery(id), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? TypedResults.Ok(ResearchJobResponse.From(result.Value))
            : ProblemResults.From(result.Error, context);
    }
}
