using Research.Providers.AnthropicWebSearch;

namespace Research.Infrastructure.UnitTests.Providers;

public sealed class WebSearchResultReaderTests
{
    [Fact]
    public void Read_CollectsHitsAcrossBlocks()
    {
        const string json = """
            {
              "content": [
                { "type": "server_tool_use", "id": "1", "name": "web_search", "input": { "query": "q" } },
                { "type": "web_search_tool_result", "tool_use_id": "1", "content": [
                    { "type": "web_search_result", "url": "https://a.example/x", "title": "A", "page_age": "2 days ago" },
                    { "type": "web_search_result", "url": "https://b.example/y" }
                ] },
                { "type": "text", "text": "summary" }
              ]
            }
            """;

        var results = WebSearchResultReader.Read(json);

        results.ErrorCodes.ShouldBeEmpty();
        results.Hits.Count.ShouldBe(2);
        results.Hits[0].Url.ShouldBe("https://a.example/x");
        results.Hits[0].Title.ShouldBe("A");
        results.Hits[0].PageAge.ShouldBe("2 days ago");
        results.Hits[1].Title.ShouldBe("https://b.example/y");
    }

    [Fact]
    public void Read_CollectsToolErrors()
    {
        const string json = """
            { "content": [ { "type": "web_search_tool_result", "content": { "type": "web_search_tool_result_error", "error_code": "max_uses_exceeded" } } ] }
            """;

        var results = WebSearchResultReader.Read(json);

        results.Hits.ShouldBeEmpty();
        results.ErrorCodes.ShouldBe(["max_uses_exceeded"]);
    }

    [Fact]
    public void Read_NoContent_IsEmpty() => WebSearchResultReader.Read("{}").Hits.ShouldBeEmpty();
}
