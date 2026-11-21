using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Research.Fetching;
using Research.Fetching.Application.Common.Interfaces;
using Research.Fetching.Domain;
using Research.Fetching.FetchPage;
using Research.Fetching.Infrastructure.Download;
using Research.Fetching.Infrastructure.Extraction;
using Research.Shared.Core.Logging;
using Research.Infrastructure.UnitTests.TestSupport;

namespace Research.Infrastructure.UnitTests.Fetching;

public sealed class FetchPagePipelineTests
{
    private static readonly string LongParagraph = string.Join(' ', Enumerable.Repeat("Readable sentence about the topic.", 30));

    [Fact]
    public async Task Html_IsStoredUnderContentHashKey()
    {
        var site = new FakeSite();
        site.Pages["/article"] = Html($"<title>T</title><p>{LongParagraph}</p>");
        var (handler, store) = Create(site);

        var result = await handler.HandleAsync(new FetchPageCommand(Guid.NewGuid(), "https://example.com/article", "Search title"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Title.ShouldBe("T");
        result.Value.BlobKey.ShouldBe($"pages/{result.Value.ContentHash}.txt");
        result.Value.ContentHash.ShouldBe(FetchPageHandler.Sha256Hex(store.Blobs[result.Value.BlobKey]));
        result.Value.CharacterCount.ShouldBe(LongParagraph.Length);
    }

    [Fact]
    public async Task RobotsDisallow_FailsWithoutDownloading()
    {
        var site = new FakeSite { Robots = "User-agent: *\nDisallow: /\n" };
        site.Pages["/article"] = Html($"<p>{LongParagraph}</p>");
        var (handler, _) = Create(site);

        var result = await handler.HandleAsync(new FetchPageCommand(Guid.NewGuid(), "https://example.com/article", null), CancellationToken.None);

        result.Error!.Code.ShouldBe(FetchingErrors.FetchDisallowed.Code);
        site.Requests.ShouldNotContain("/article");
    }

    [Fact]
    public async Task NotFound_IsPermanentHttpError()
    {
        var (handler, _) = Create(new FakeSite());
        var result = await handler.HandleAsync(new FetchPageCommand(Guid.NewGuid(), "https://example.com/missing", null), CancellationToken.None);
        result.Error!.Code.ShouldBe(FetchingErrors.HttpError.Code);
    }

    [Fact]
    public async Task ServerError_Throws_SoTheMessageIsRetried()
    {
        var site = new FakeSite();
        site.Status["/flaky"] = HttpStatusCode.ServiceUnavailable;
        var (handler, _) = Create(site);

        await Should.ThrowAsync<HttpRequestException>(() =>
            handler.HandleAsync(new FetchPageCommand(Guid.NewGuid(), "https://example.com/flaky", null), CancellationToken.None));
    }

    [Fact]
    public async Task ShortPage_IsContentTooShort()
    {
        var site = new FakeSite();
        site.Pages["/short"] = Html("<p>Too short.</p>");
        var (handler, _) = Create(site);

        var result = await handler.HandleAsync(new FetchPageCommand(Guid.NewGuid(), "https://example.com/short", null), CancellationToken.None);
        result.Error!.Code.ShouldBe(FetchingErrors.ContentTooShort.Code);
    }

    [Fact]
    public async Task Pdf_IsUnsupported()
    {
        var site = new FakeSite();
        site.Pages["/doc.pdf"] = new ByteArrayContent([1, 2, 3]) { Headers = { ContentType = new("application/pdf") } };
        var (handler, _) = Create(site);

        var result = await handler.HandleAsync(new FetchPageCommand(Guid.NewGuid(), "https://example.com/doc.pdf", null), CancellationToken.None);
        result.Error!.Code.ShouldBe(FetchingErrors.UnsupportedContent.Code);
    }

    [Theory]
    [InlineData("ftp://example.com/file")]
    [InlineData("not a url")]
    [InlineData("/relative")]
    public async Task InvalidUrl_IsRejected(string url)
    {
        var (handler, _) = Create(new FakeSite());
        var result = await handler.HandleAsync(new FetchPageCommand(Guid.NewGuid(), url, null), CancellationToken.None);
        result.Error!.Code.ShouldBe(FetchingErrors.InvalidUrl.Code);
    }

    private static StringContent Html(string body) => new($"<html><body>{body}</body></html>", Encoding.UTF8, "text/html");

    private static (FetchPageHandler Handler, InMemoryDocumentStore Store) Create(FakeSite site)
    {
        var options = TestOptions.Of(new FetchingOptions { MinContentChars = 200 });
        var clients = new FakeHttpClientFactory(site);
        var time = new FakeTimeProvider();
        var store = new InMemoryDocumentStore();
        var handler = new FetchPageHandler(
            new RobotsPolicy(clients, options, time),
            new DomainThrottle(options),
            new PageDownloader(clients, options),
            new ContentExtractor(),
            store,
            new FetchingMetrics(TestMeters.Create()),
            options,
            TestOptions.Of(new ContentLoggingOptions()),
            time,
            NullLoggerFactory.Instance);
        return (handler, store);
    }

    private sealed class FakeSite : HttpMessageHandler
    {
        public Dictionary<string, HttpContent> Pages { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, HttpStatusCode> Status { get; } = new(StringComparer.Ordinal);

        public List<string> Requests { get; } = [];

        public string? Robots { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add(path);

            if (path == "/robots.txt")
            {
                return Task.FromResult(Robots is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Robots), RequestMessage = request });
            }

            if (Status.TryGetValue(path, out var status))
            {
                return Task.FromResult(new HttpResponseMessage(status) { RequestMessage = request });
            }

            return Task.FromResult(Pages.TryGetValue(path, out var content)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = request }
                : new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
        }
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
