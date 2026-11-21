using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Research.Shared.Web.Endpoints;

namespace Research.Knowledge.Api.Endpoints;

/// <summary>The internal HTTP surface of Knowledge, discovered via <see cref="IEndpointGroup"/>.</summary>
public sealed class KnowledgeEndpoints : IEndpointGroup
{
    public static string RoutePrefix => "/knowledge";

    public static string Tag => "Knowledge";

    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost(SearchPassagesEndpoint.Pattern, SearchPassagesEndpoint.HandleAsync)
            .WithName(SearchPassagesEndpoint.Name)
            .WithSummary("Find the passages of a job's documents most similar to the queries")
            .Accepts<SearchPassagesRequest>("application/json")
            .Produces<SearchPassagesResponse>()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);
    }
}
