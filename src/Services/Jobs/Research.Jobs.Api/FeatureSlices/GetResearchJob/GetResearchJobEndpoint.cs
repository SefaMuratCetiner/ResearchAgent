using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Research.Jobs.Api.Common.Models;
using Research.Jobs.Application.GetResearchJob;
using Research.Shared.Bus.Mediation;
using Research.Shared.Web.Endpoints;
using Research.Shared.Web.Problems;

namespace Research.Jobs.Api.GetResearchJob;

/// <summary><c>GET /research/{id}</c>: the job, its plan, warnings and step progress.</summary>
public sealed class GetResearchJobEndpoint : IEndpoint
{
    public const string Pattern = "/{id:guid}";
    public const string Name = "GetResearchJob";

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet(Pattern, HandleAsync)
            .WithName(Name)
            .WithSummary("Get a research job and its progress")
            .Produces<ResearchJobResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    public static async Task<IResult> HandleAsync(Guid id, ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sender);
        var result = await sender.SendAsync(new GetResearchJobQuery(id), cancellationToken).ConfigureAwait(false);
        return result.ToOkOrProblem(context, ResearchJobResponse.From);
    }
}
