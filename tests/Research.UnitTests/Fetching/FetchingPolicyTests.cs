using System.Net;
using System.Text;
using Research.Fetching;
using Research.Fetching.Download;
using Research.Fetching.Extraction;

namespace Research.UnitTests.Fetching;

public sealed class SsrfGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("ff02::1")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("2001:db8::1")]
    public void IsPublic_PrivateOrReserved_IsFalse(string address) =>
        SsrfGuard.IsPublic(IPAddress.Parse(address)).ShouldBeFalse();

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("172.32.0.1")]
    [InlineData("100.128.0.1")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("::ffff:8.8.8.8")]
    public void IsPublic_PublicAddress_IsTrue(string address) =>
        SsrfGuard.IsPublic(IPAddress.Parse(address)).ShouldBeTrue();
}

public sealed class RobotsRulesTests
{
    private const string Robots = """
        # comment
        User-agent: *
        Disallow: /private
        Allow: /private/open

        User-agent: OtherBot
        Disallow: /
        """;

    [Theory]
    [InlineData("/", true)]
    [InlineData("/public/page", true)]
    [InlineData("/private", false)]
    [InlineData("/private/secret", false)]
    [InlineData("/private/open/page", true)]
    public void Wildcard_LongestMatchWins(string path, bool allowed) =>
        RobotsRules.Parse(Robots, "ResearchAgent").IsAllowed(path).ShouldBe(allowed);

    [Fact]
    public void SpecificGroup_ReplacesWildcard() =>
        RobotsRules.Parse(Robots, "OtherBot").IsAllowed("/public").ShouldBeFalse();

    [Fact]
    public void EmptyDisallow_AllowsEverything() =>
        RobotsRules.Parse("User-agent: *\nDisallow:\n", "ResearchAgent").IsAllowed("/anything").ShouldBeTrue();

    [Fact]
    public void GroupedUserAgents_ShareRules()
    {
        var rules = RobotsRules.Parse("User-agent: a\nUser-agent: *\nDisallow: /x\n", "ResearchAgent");
        rules.IsAllowed("/x/y").ShouldBeFalse();
    }
}

public sealed class ContentExtractorTests
{
    [Fact]
    public void Html_KeepsArticleTextAndDropsNoise()
    {
        const string html = """
            <html><head><title> The Title </title><script>var x = 1;</script></head>
            <body>
              <nav>Home | About</nav>
              <article>
                <h1>Heading</h1>
                <p>First   paragraph
                   text.</p>
                <ul><li><p>Item one</p></li></ul>
                <aside>Related links</aside>
              </article>
              <footer>Copyright</footer>
            </body></html>
            """;

        var result = ContentExtractor.Extract(Encoding.UTF8.GetBytes(html), "text/html", "utf-8", "fallback");

        result.Title.ShouldBe("The Title");
        result.Text.ShouldBe("Heading\n\nFirst paragraph text.\n\nItem one");
        result.Text.ShouldNotContain("Home");
        result.Text.ShouldNotContain("Related");
        result.Text.ShouldNotContain("Copyright");
        result.Text.ShouldNotContain("var x");
    }

    [Fact]
    public void Html_WithoutTitle_UsesFallback()
    {
        var result = ContentExtractor.Extract(Encoding.UTF8.GetBytes("<p>Body text</p>"), "text/html", null, "fallback");
        result.Title.ShouldBe("fallback");
        result.Text.ShouldBe("Body text");
    }

    [Fact]
    public void PlainText_IsNormalized()
    {
        var result = ContentExtractor.Extract(Encoding.UTF8.GetBytes("  a\n\n  b\tc  "), "text/plain", "utf-8", "title");
        result.Text.ShouldBe("a b c");
        result.Title.ShouldBe("title");
    }
}

public sealed class FetchingOptionsValidatorTests
{
    [Fact]
    public void Defaults_AreValid() =>
        new FetchingOptionsValidator().Validate(null, new FetchingOptions()).Succeeded.ShouldBeTrue();

    [Fact]
    public void MinAboveMax_Fails() =>
        new FetchingOptionsValidator().Validate(null, new FetchingOptions { MinContentChars = 10, MaxContentChars = 5 })
            .Failed.ShouldBeTrue();
}
