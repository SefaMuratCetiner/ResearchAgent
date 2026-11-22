using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Research.Knowledge.Application.SearchPassages;
using Research.Shared.Core.Mediation;
using Research.Shared.Web.Endpoints;
using Research.Shared.Web.Problems;

namespace Research.Knowledge.Api.Endpoints;

/// <summary>Body of <c>POST /knowledge/passages/search</c>.</summary>
public sealed record SearchPassagesRequest(Guid? JobId, IReadOnlyList<string>? Queries, int? TopK);

/// <summary>The matching passages, best first.</summary>
public sealed record SearchPassagesResponse(IReadOnlyList<PassageResponse> Passages);

/// <summary>One passage; <see cref="Score"/> is cosine similarity.</summary>
public sealed record PassageResponse(Guid DocumentId, string Url, string Title, string Text, double Score);

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
            .WithRequestValidation<SearchPassagesRequest, Validator>()
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

    /// <summary>
    /// Stage-1 HTTP shape validation: ensures required body fields are present before the endpoint executes.
    /// Policy bounds (<c>MaxQueries</c>, <c>MaxQueryLength</c>, <c>MaxTopK</c>) run in Stage-2
    /// (<see cref="SearchPassagesQueryValidator"/>) via <c>ValidationBehavior</c>.
    /// </summary>
    public sealed class Validator : IEndpointValidator<SearchPassagesRequest>
    {
        public void Validate(SearchPassagesRequest? request, HttpContext httpContext, IDictionary<string, string[]> errors)
        {
            if (request?.JobId is null || request.JobId == Guid.Empty)
            {
                errors["jobId"] = ["The job id is required."];
            }

            var hasQuery = request?.Queries?.Any(static q => !string.IsNullOrWhiteSpace(q)) ?? false;
            if (!hasQuery)
            {
                errors["queries"] = ["At least one non-empty query is required."];
            }

            if (request?.TopK is not { } topK || topK < 1)
            {
                errors["topK"] = ["TopK must be at least 1."];
            }
        }
    }
}
