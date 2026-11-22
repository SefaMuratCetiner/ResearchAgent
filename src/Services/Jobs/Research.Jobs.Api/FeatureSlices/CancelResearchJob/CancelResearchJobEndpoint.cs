using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Research.Jobs.Api.Common.Models;
using Research.Jobs.Application.CancelResearchJob;
using Research.Shared.Bus.Mediation;
using Research.Shared.Web.Endpoints;
using Research.Shared.Web.Problems;

namespace Research.Jobs.Api.CancelResearchJob;

/// <summary><c>POST /research/{id}/cancel</c>: cancels an active job; cancelling twice is fine.</summary>
public sealed class CancelResearchJobEndpoint : IEndpoint
{
    public const string Pattern = "/{id:guid}/cancel";
    public const string Name = "CancelResearchJob";

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(Pattern, HandleAsync)
            .WithName(Name)
            .WithSummary("Cancel a research job")
            .Produces<ResearchJobResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    public static async Task<IResult> HandleAsync(Guid id, ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sender);
        var result = await sender.SendAsync(new CancelResearchJobCommand(id), cancellationToken).ConfigureAwait(false);
        return result.ToOkOrProblem(context, static job => ResearchJobResponse.From(job));
    }
}
