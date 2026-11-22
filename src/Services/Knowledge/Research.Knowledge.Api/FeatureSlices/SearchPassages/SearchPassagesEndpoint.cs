using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Research.Knowledge.Application.SearchPassages;
using Research.Shared.Bus.Mediation;
using Research.Shared.Web.Endpoints;
using Research.Shared.Web.Problems;

namespace Research.Knowledge.Api.SearchPassages;

/// <summary>
/// <c>POST /knowledge/passages/search</c>: the <c>topK</c> passages of the job's documents closest to any query. A job
/// without documents returns an empty list.
/// </summary>
public sealed class SearchPassagesEndpoint : IEndpoint
{
    public const string Pattern = "/passages/search";
    public const string Name = "SearchPassages";

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost(Pattern, HandleAsync)
            .WithName(Name)
            .WithSummary("Find the passages of a job's documents most similar to the queries")
            .WithRequestValidation<SearchPassagesRequest, SearchPassagesRequestValidator>()
            .Accepts<SearchPassagesRequest>("application/json")
            .Produces<SearchPassagesResponse>()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

    public static async Task<IResult> HandleAsync(
        SearchPassagesRequest? request,
        ISender sender,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(context);

        var queries = request!.Queries!.Where(static q => !string.IsNullOrWhiteSpace(q)).ToList();
        var result = await sender.SendAsync(new SearchPassagesQuery(request.JobId!.Value, queries, request.TopK!.Value), cancellationToken)
            .ConfigureAwait(false);

        return result.ToOkOrProblem(context, static matches => new SearchPassagesResponse(
            matches.Select(static m => new PassageResponse(m.DocumentId, m.Url, m.Title, m.Text, m.Score)).ToList()));
    }
}
