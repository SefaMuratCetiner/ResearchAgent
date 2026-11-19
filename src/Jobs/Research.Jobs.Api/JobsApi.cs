using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Research.Jobs.Api.Endpoints;
using Research.Problems;

namespace Research.Jobs.Api;

/// <summary>The HTTP surface of Jobs. The Host calls <see cref="Map"/> once, after the pipeline.</summary>
public static class JobsApi
{
    public const string Tag = "Research";

    public static void Map(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var research = app.MapGroup("/research")
            .WithTags(Tag)
            .ProducesProblem(StatusCodes.Status500InternalServerError, ResearchProblem.ContentType);

        research.MapPost(CreateResearchJobEndpoint.Pattern, CreateResearchJobEndpoint.HandleAsync)
            .WithName(CreateResearchJobEndpoint.Name)
            .WithSummary("Start a research job")
            .Accepts<CreateResearchJobRequest>("application/json")
            .Produces<ResearchJobResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest, ResearchProblem.ContentType)
            .ProducesProblem(StatusCodes.Status409Conflict, ResearchProblem.ContentType);

        research.MapGet(GetResearchJobEndpoint.Pattern, GetResearchJobEndpoint.HandleAsync)
            .WithName(GetResearchJobEndpoint.Name)
            .WithSummary("Get a research job and its progress")
            .Produces<ResearchJobResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound, ResearchProblem.ContentType);

        research.MapGet(GetResearchReportEndpoint.Pattern, GetResearchReportEndpoint.HandleAsync)
            .WithName(GetResearchReportEndpoint.Name)
            .WithSummary("Get the report of a completed research job")
            .Produces<ResearchReportResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound, ResearchProblem.ContentType)
            .ProducesProblem(StatusCodes.Status409Conflict, ResearchProblem.ContentType);

        research.MapPost(CancelResearchJobEndpoint.Pattern, CancelResearchJobEndpoint.HandleAsync)
            .WithName(CancelResearchJobEndpoint.Name)
            .WithSummary("Cancel a research job")
            .Produces<ResearchJobResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound, ResearchProblem.ContentType)
            .ProducesProblem(StatusCodes.Status409Conflict, ResearchProblem.ContentType);

        research.MapGet(ResearchJobEventsEndpoint.Pattern, ResearchJobEventsEndpoint.HandleAsync)
            .WithName(ResearchJobEventsEndpoint.Name)
            .WithSummary("Stream status changes of a research job (Server-Sent Events)")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblem(StatusCodes.Status404NotFound, ResearchProblem.ContentType);

        // The OpenAPI document, outside Production only: clients use the committed docs/openapi/jobs-v1.json.
        if (!app.Environment.IsProduction())
        {
            app.MapOpenApi();
        }
    }
}
