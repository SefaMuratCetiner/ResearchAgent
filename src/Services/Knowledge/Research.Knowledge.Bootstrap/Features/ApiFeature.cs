using System.Text.Json.Serialization;
using Research.ServiceDefaults.Composition;

namespace Research.Knowledge.Bootstrap.Features;

/// <summary>The HTTP API's services: the OpenAPI document and the JSON shape of responses.</summary>
internal sealed class ApiFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Services.AddOpenApi();
        app.Services.ConfigureHttpJsonOptions(static options =>
        {
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
    }
}
