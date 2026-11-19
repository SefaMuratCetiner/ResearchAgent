using Research.Search.AnthropicWebSearch;
using Research.Search.RunSearch;

namespace Research.UnitTests.Search;

public sealed class UrlCanonicalizerTests
{
    [Theory]
    [InlineData("https://Example.COM/Path/", "https://example.com/Path")]
    [InlineData("https://example.com:443/a#section", "https://example.com/a")]
    [InlineData("http://example.com:8080/a", "http://example.com:8080/a")]
    [InlineData("https://example.com/a?utm_source=x&id=7&fbclid=y", "https://example.com/a?id=7")]
    [InlineData("https://example.com", "https://example.com/")]
    [InlineData(" https://example.com/a ", "https://example.com/a")]
    public void Canonicalize_NormalizesSpelling(string input, string expected) =>
        UrlCanonicalizer.Canonicalize(input).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ftp://example.com/file")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("not a url")]
    public void Canonicalize_RejectsNonHttp(string? input) => UrlCanonicalizer.Canonicalize(input).ShouldBeNull();
}

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
