using System.Text.Json.Serialization;
using Research.Shared.Adapters.Composition;
using Research.Shared.Web;

namespace Research.Jobs.Bootstrap.Features;

/// <summary>The HTTP API's services: ProblemDetails, exception handling, the OpenAPI document and JSON serialization.</summary>
internal sealed class ApiFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Services.AddResearchProblemDetails();
        app.Services.AddOpenApi();
        app.Services.ConfigureHttpJsonOptions(static options =>
        {
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
    }
}
