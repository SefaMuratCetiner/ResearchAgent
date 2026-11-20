using Microsoft.AspNetCore.Http;
using Research.Jobs.CancelResearchJob;
using Research.ServiceDefaults.Middleware;
using Research.ServiceDefaults.Problems;

namespace Research.Jobs.Api.Endpoints;

/// <summary><c>POST /research/{id}/cancel</c>: cancels an active job; cancelling twice is fine.</summary>
public static class CancelResearchJobEndpoint
{
    public const string Pattern = "/{id:guid}/cancel";
    public const string Name = "CancelResearchJob";

    public static async Task<IResult> HandleAsync(Guid id, CancelResearchJobHandler handler, HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var result = await handler.HandleAsync(new CancelResearchJobCommand(id), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? TypedResults.Ok(ResearchJobResponse.From(result.Value))
            : ProblemResults.From(result.Error, context);
    }
}
