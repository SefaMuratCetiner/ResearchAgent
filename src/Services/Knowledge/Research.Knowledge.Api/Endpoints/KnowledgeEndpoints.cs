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

        SearchPassagesEndpoint.Map(group);
    }
}
