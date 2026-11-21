using System.Text.Json.Serialization;
using Research.ServiceDefaults.Composition;
using Research.ServiceDefaults.Problems;

namespace Research.Jobs.Bootstrap.Features;

/// <summary>The HTTP API's services: OpenAPI, built-in ProblemDetails + IExceptionHandler and JSON serialization.</summary>
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
