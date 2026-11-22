using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Research.Jobs.Application.GetResearchReport;
using Research.Shared.Core.Mediation;
using Research.Shared.Web.Endpoints;
using Research.Shared.Web.Problems;

namespace Research.Jobs.Api.GetResearchReport;

/// <summary>
/// <c>GET /research/{id}/report</c>: the report in Markdown with numbered citations. <c>409 jobs.report_not_ready</c>
/// while the job runs; <c>404 jobs.report_not_available</c> when it ended without one.
/// </summary>
public sealed class GetResearchReportEndpoint : IEndpoint
{
    public const string Pattern = "/{id:guid}/report";
    public const string Name = "GetResearchReport";

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet(Pattern, HandleAsync)
            .WithName(Name)
            .WithSummary("Get the report of a completed research job")
            .Produces<ResearchReportResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

    public static async Task<IResult> HandleAsync(Guid id, ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sender);
        var result = await sender.SendAsync(new GetResearchReportQuery(id), cancellationToken).ConfigureAwait(false);
        return result.ToOkOrProblem(context, ResearchReportResponse.From);
    }
}
