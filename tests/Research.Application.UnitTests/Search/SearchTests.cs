using Research.Search.Application.RunSearch;

namespace Research.Application.UnitTests.Search;

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
