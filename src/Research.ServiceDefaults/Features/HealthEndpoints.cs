using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;

namespace Research.ServiceDefaults.Features;

/// <summary>
/// <c>/health/live</c> runs no checks (the process answers); <c>/health/ready</c> runs every registered check.
/// Degraded checks report 200, so a cache outage does not take a replica out of rotation. Status only, no details.
/// </summary>
public static class HealthEndpoints
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = static _ => false });
        endpoints.MapHealthChecks(ReadyPath);
    }
}
