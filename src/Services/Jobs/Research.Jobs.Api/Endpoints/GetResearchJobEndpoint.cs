using Microsoft.AspNetCore.Http;
using Research.Jobs.Application.GetResearchJob;
using Research.Shared.Core.Mediation;
using Research.Shared.Web.Problems;

namespace Research.Jobs.Api.Endpoints;

/// <summary><c>GET /research/{id}</c>: the job, its plan, warnings and step progress.</summary>
public static class GetResearchJobEndpoint
{
    public const string Pattern = "/{id:guid}";
    public const string Name = "GetResearchJob";

    public static async Task<IResult> HandleAsync(Guid id, ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sender);
        var result = await sender.SendAsync(new GetResearchJobQuery(id), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? TypedResults.Ok(ResearchJobResponse.From(result.Value))
            : ProblemResults.From(result.Error, context);
    }
}
