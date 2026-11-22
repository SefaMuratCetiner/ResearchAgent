using Microsoft.AspNetCore.Http;
using Research.Knowledge.Application.SearchPassages;
using Research.Shared.Web.Endpoints;

namespace Research.Knowledge.Api.SearchPassages;

/// <summary>
/// Stage-1 HTTP shape validation: ensures required body fields are present before the endpoint executes.
/// Policy bounds (<c>MaxQueries</c>, <c>MaxQueryLength</c>, <c>MaxTopK</c>) run in Stage-2
/// (<see cref="SearchPassagesQueryValidator"/>) via <c>ValidationBehavior</c>.
/// </summary>
public sealed class SearchPassagesRequestValidator : IEndpointValidator<SearchPassagesRequest>
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
