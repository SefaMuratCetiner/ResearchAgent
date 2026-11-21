using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Research.Shared.Web.Endpoints;

namespace Research.Jobs.Api.Endpoints;

/// <summary>The public HTTP surface of Jobs, discovered via <see cref="IEndpointGroup"/>.</summary>
public sealed class JobsEndpoints : IEndpointGroup
{
    public static string RoutePrefix => "/research";

    public static string Tag => "Research";

    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost(CreateResearchJobEndpoint.Pattern, CreateResearchJobEndpoint.HandleAsync)
            .WithName(CreateResearchJobEndpoint.Name)
            .WithSummary("Start a research job")
            .Accepts<CreateResearchJobRequest>("application/json")
            .Produces<ResearchJobResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet(GetResearchJobEndpoint.Pattern, GetResearchJobEndpoint.HandleAsync)
            .WithName(GetResearchJobEndpoint.Name)
            .WithSummary("Get a research job and its progress")
            .Produces<ResearchJobResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet(GetResearchReportEndpoint.Pattern, GetResearchReportEndpoint.HandleAsync)
            .WithName(GetResearchReportEndpoint.Name)
            .WithSummary("Get the report of a completed research job")
            .Produces<ResearchReportResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost(CancelResearchJobEndpoint.Pattern, CancelResearchJobEndpoint.HandleAsync)
            .WithName(CancelResearchJobEndpoint.Name)
            .WithSummary("Cancel a research job")
            .Produces<ResearchJobResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet(ResearchJobEventsEndpoint.Pattern, ResearchJobEventsEndpoint.HandleAsync)
            .WithName(ResearchJobEventsEndpoint.Name)
            .WithSummary("Stream status changes of a research job (Server-Sent Events)")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
