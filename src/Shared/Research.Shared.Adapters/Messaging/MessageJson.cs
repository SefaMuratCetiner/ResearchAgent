using System.Text.Json;
using System.Text.Json.Serialization;

namespace Research.Shared.Adapters.Messaging;

/// <summary>The one JSON shape of every message payload: camelCase, enums as strings, unknown fields ignored.</summary>
public static class MessageJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
