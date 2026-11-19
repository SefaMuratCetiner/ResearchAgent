using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Research.Knowledge.Api.Endpoints;
using Research.Problems;

namespace Research.Knowledge.Api;

/// <summary>The internal HTTP surface of Knowledge. The Host calls <see cref="Map"/> once, after the pipeline.</summary>
public static class KnowledgeApi
{
    public const string Tag = "Knowledge";

    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var knowledge = app.MapGroup("/knowledge")
            .WithTags(Tag)
            .ProducesProblem(StatusCodes.Status500InternalServerError, ResearchProblem.ContentType);

        knowledge.MapPost(SearchPassagesEndpoint.Pattern, SearchPassagesEndpoint.HandleAsync)
            .WithName(SearchPassagesEndpoint.Name)
            .WithSummary("Find the passages of a job's documents most similar to the queries")
            .Accepts<SearchPassagesRequest>("application/json")
            .Produces<SearchPassagesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest, ResearchProblem.ContentType);

        if (!app.Environment.IsProduction())
        {
            app.MapOpenApi();
        }
    }
}
