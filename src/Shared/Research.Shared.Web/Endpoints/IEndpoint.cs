using Microsoft.AspNetCore.Routing;

namespace Research.Shared.Web.Endpoints;

/// <summary>
/// Contract for a self-contained REPR (Request-Endpoint-Response) Minimal API endpoint mapped onto a
/// <see cref="RouteGroupBuilder"/> by its owning <see cref="IEndpointGroup"/>.
/// </summary>
public interface IEndpoint
{
    static abstract void Map(RouteGroupBuilder group);
}
