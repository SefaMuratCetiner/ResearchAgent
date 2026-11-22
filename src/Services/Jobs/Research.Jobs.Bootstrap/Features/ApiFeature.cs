using Research.Shared.Adapters.Composition;
using Research.Shared.Web;

namespace Research.Jobs.Bootstrap.Features;

/// <summary>The HTTP API's services: ProblemDetails, exception handling, the OpenAPI document and JSON serialization.</summary>
internal sealed class ApiFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Services.AddResearchHttpApiDefaults();
        app.Services.AddOpenApi();
    }
}
