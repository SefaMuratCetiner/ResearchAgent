using Research.Shared.Core.Hosting;
using Research.Shared.Web;

namespace Research.Jobs.Bootstrap.Features;

/// <summary>The HTTP API's services: ProblemDetails, exception handling, the OpenAPI document and JSON serialization.</summary>
internal sealed class ApiFeature : IHostFeature
{
    public void Register(ServiceHostBuilder app)
    {
        app.Services.AddResearchHttpApiDefaults();
        app.Services.AddOpenApi();
    }
}
