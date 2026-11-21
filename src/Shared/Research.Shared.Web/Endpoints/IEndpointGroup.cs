using Microsoft.AspNetCore.Routing;

namespace Research.Shared.Web.Endpoints;

/// <summary>
/// Contract for a Minimal API endpoint group discovered automatically by
/// <see cref="EndpointGroupExtensions.MapEndpoints(Microsoft.AspNetCore.Builder.WebApplication, System.Reflection.Assembly)"/>.
/// </summary>
public interface IEndpointGroup
{
    /// <summary>Route prefix for every endpoint in the group (e.g. <c>/research</c>).</summary>
    static abstract string RoutePrefix { get; }

    /// <summary>OpenAPI tag applied to every endpoint in the group.</summary>
    static abstract string Tag { get; }

    /// <summary>Maps all endpoints belonging to this group onto <paramref name="group"/>.</summary>
    static abstract void Map(RouteGroupBuilder group);
}
