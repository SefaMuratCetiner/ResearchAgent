using Microsoft.AspNetCore.Http;
using Research.Jobs.Application.CancelResearchJob;
using Research.Shared.Core.Mediation;
using Research.Shared.Web.Problems;

namespace Research.Jobs.Api.Endpoints;

/// <summary><c>POST /research/{id}/cancel</c>: cancels an active job; cancelling twice is fine.</summary>
public static class CancelResearchJobEndpoint
{
    public const string Pattern = "/{id:guid}/cancel";
    public const string Name = "CancelResearchJob";

    public static async Task<IResult> HandleAsync(Guid id, ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sender);
        var result = await sender.SendAsync(new CancelResearchJobCommand(id), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? TypedResults.Ok(ResearchJobResponse.From(result.Value))
            : ProblemResults.From(result.Error, context);
    }
}
