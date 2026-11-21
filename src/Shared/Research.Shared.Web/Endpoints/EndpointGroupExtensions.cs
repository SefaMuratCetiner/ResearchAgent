using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Research.Shared.Web.Endpoints;

/// <summary>
/// Discovers and maps all <see cref="IEndpointGroup"/> implementations in an API assembly onto a
/// <see cref="WebApplication"/>.
/// </summary>
public static class EndpointGroupExtensions
{
    private static readonly MethodInfo MapGroupMethod = typeof(EndpointGroupExtensions)
        .GetMethod(nameof(MapGroup), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Maps every concrete <see cref="IEndpointGroup"/> in <typeparamref name="TMarker"/>'s assembly.</summary>
    public static WebApplication MapEndpoints<TMarker>(this WebApplication app) =>
        app.MapEndpoints(typeof(TMarker).Assembly);

    /// <summary>Maps every concrete <see cref="IEndpointGroup"/> in <paramref name="assembly"/>.</summary>
    public static WebApplication MapEndpoints(this WebApplication app, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(assembly);

        var groupTypes = assembly.GetTypes()
            .Where(static t => t is { IsClass: true, IsAbstract: false } && typeof(IEndpointGroup).IsAssignableFrom(t))
            .OrderBy(static t => t.FullName, StringComparer.Ordinal);

        foreach (var groupType in groupTypes)
        {
            MapGroupMethod.MakeGenericMethod(groupType).Invoke(null, [app]);
        }

        return app;
    }

    private static void MapGroup<TGroup>(WebApplication app)
        where TGroup : IEndpointGroup
    {
        var group = app.MapGroup(TGroup.RoutePrefix)
            .WithTags(TGroup.Tag)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        TGroup.Map(group);
    }
}
