using System.Text.Json;
using Research.Shared.Core.Search;

namespace Research.Providers.AnthropicWebSearch;

/// <summary>
/// Reads <c>web_search_tool_result</c> blocks from a Messages API response in its wire (JSON) shape:
/// <code>
/// { "type": "web_search_tool_result", "content": [ { "type": "web_search_result", "url", "title", "page_age" } ] }
/// { "type": "web_search_tool_result", "content": { "type": "web_search_tool_result_error", "error_code" } }
/// </code>
/// Working on JSON keeps this independent of how the SDK names the result-block types (VERIFY on the first build that
/// <see cref="JsonSerializer"/> writes SDK responses in wire shape; if not, switch to the SDK's typed union).
/// </summary>
public static class WebSearchResultReader
{
    public static WebSearchResults Read(string responseJson)
    {
        var hits = new List<SearchHit>();
        var errors = new List<string>();

        using var document = JsonDocument.Parse(responseJson);
        if (!document.RootElement.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return new WebSearchResults(hits, errors);
        }

        foreach (var block in content.EnumerateArray())
        {
            if (Text(block, "type") != "web_search_tool_result" || !block.TryGetProperty("content", out var result))
            {
                continue;
            }

            if (result.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in result.EnumerateArray())
                {
                    if (Text(item, "type") == "web_search_result" && Text(item, "url") is { Length: > 0 } url)
                    {
                        hits.Add(new SearchHit(url, Text(item, "title") ?? url, Text(item, "page_age")));
                    }
                }
            }
            else if (result.ValueKind == JsonValueKind.Object && Text(result, "error_code") is { } errorCode)
            {
                errors.Add(errorCode);
            }
        }

        return new WebSearchResults(hits, errors);
    }

    private static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

/// <summary>Hits in order of appearance, and the error codes of failed searches.</summary>
public sealed record WebSearchResults(IReadOnlyList<SearchHit> Hits, IReadOnlyList<string> ErrorCodes);
