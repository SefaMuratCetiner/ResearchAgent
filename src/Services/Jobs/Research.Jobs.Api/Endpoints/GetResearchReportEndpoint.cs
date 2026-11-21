using Microsoft.AspNetCore.Http;
using Research.Jobs.Application.GetResearchReport;
using Research.Shared.Web.Problems;

namespace Research.Jobs.Api.Endpoints;

/// <summary>
/// <c>GET /research/{id}/report</c>: the report in Markdown with numbered citations. <c>409 jobs.report_not_ready</c>
/// while the job runs; <c>404 jobs.report_not_available</c> when it ended without one.
/// </summary>
public static class GetResearchReportEndpoint
{
    public const string Pattern = "/{id:guid}/report";
    public const string Name = "GetResearchReport";

    public static async Task<IResult> HandleAsync(Guid id, GetResearchReportHandler handler, HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var result = await handler.HandleAsync(new GetResearchReportQuery(id), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? TypedResults.Ok(ResearchReportResponse.From(result.Value))
            : ProblemResults.From(result.Error, context);
    }
}
