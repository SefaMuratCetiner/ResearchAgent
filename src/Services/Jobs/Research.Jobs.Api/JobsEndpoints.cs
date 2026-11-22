using Microsoft.AspNetCore.Routing;
using Research.Jobs.Api.CancelResearchJob;
using Research.Jobs.Api.CreateResearchJob;
using Research.Jobs.Api.GetResearchJob;
using Research.Jobs.Api.GetResearchReport;
using Research.Jobs.Api.StreamResearchJobEvents;
using Research.Shared.Web.Endpoints;

namespace Research.Jobs.Api;

/// <summary>The public HTTP surface of Jobs, discovered via <see cref="IEndpointGroup"/>.</summary>
public sealed class JobsEndpoints : IEndpointGroup
{
    public static string RoutePrefix => "/research";

    public static string Tag => "Research";

    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        CreateResearchJobEndpoint.Map(group);
        GetResearchJobEndpoint.Map(group);
        GetResearchReportEndpoint.Map(group);
        CancelResearchJobEndpoint.Map(group);
        ResearchJobEventsEndpoint.Map(group);
    }
}
