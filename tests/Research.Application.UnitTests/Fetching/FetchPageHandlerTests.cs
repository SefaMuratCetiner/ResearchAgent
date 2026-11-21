using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Research.Application.UnitTests.TestSupport;
using Research.Fetching.Application;
using Research.Fetching.Application.Common.Interfaces;
using Research.Fetching.Domain;
using Research.Fetching.Application.FetchPage;
using Research.Shared.Core.Logging;
using Research.SharedKernel.Results;

namespace Research.Application.UnitTests.Fetching;

/// <summary>Pure Application tests for <see cref="FetchPageHandler"/> using fakes of every I/O port.</summary>
public sealed class FetchPageHandlerTests
{
    private static readonly string LongParagraph = string.Join(' ', Enumerable.Repeat("Readable sentence about the topic.", 30));

    [Fact]
    public async Task ValidPage_IsExtractedAndStoredUnderContentHashKey()
    {
        var store = new InMemoryDocumentStore();
        var handler = Create(store, extracted: new ExtractedContent("Page Title", LongParagraph));

        var result = await handler.HandleAsync(new FetchPageCommand(Guid.NewGuid(), "https://example.com/article", "Fallback"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Title.ShouldBe("Page Title");
        result.Value.BlobKey.ShouldBe($"pages/{result.Value.ContentHash}.txt");
        store.Blobs[result.Value.BlobKey].ShouldBe(LongParagraph);
    }

    [Fact]
    public async Task RobotsDisallow_FailsWithoutDownloading()
    {
        var downloader = new FakePageDownloader(Result<DownloadedPage>.Success(SamplePage()));
        var handler = Create(new InMemoryDocumentStore(), robotsAllowed: false, downloader: downloader);

        var result = await handler.HandleAsync(new FetchPageCommand(Guid.NewGuid(), "https://example.com/article", null), CancellationToken.None);

        result.Error!.Code.ShouldBe(FetchingErrors.FetchDisallowed.Code);
        downloader.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task BlockedDomain_FailsBeforeCheckingRobots()
    {
        var downloader = new FakePageDownloader(Result<DownloadedPage>.Success(SamplePage()));
        var handler = Create(
            new InMemoryDocumentStore(),
            options: new FetchingOptions { BlockedDomains = ["blocked.example"] },
            downloader: downloader);

        var result = await handler.HandleAsync(new FetchPageCommand(Guid.NewGuid(), "https://sub.blocked.example/page", null), CancellationToken.None);

        result.Error!.Code.ShouldBe(FetchingErrors.BlockedDomain.Code);
        downloader.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task DownloadFailure_PropagatesError()
    {
        var downloader = new FakePageDownloader(Result<DownloadedPage>.Failure(FetchingErrors.HttpError));
        var handler = Create(new InMemoryDocumentStore(), downloader: downloader);

        var result = await handler.HandleAsync(new FetchPageCommand(Guid.NewGuid(), "https://example.com/missing", null), CancellationToken.None);

        result.Error!.Code.ShouldBe(FetchingErrors.HttpError.Code);
    }

    [Fact]
    public async Task ShortExtractedText_IsContentTooShort()
    {
        var handler = Create(new InMemoryDocumentStore(), extracted: new ExtractedContent("T", "Too short."));

        var result = await handler.HandleAsync(new FetchPageCommand(Guid.NewGuid(), "https://example.com/short", null), CancellationToken.None);

        result.Error!.Code.ShouldBe(FetchingErrors.ContentTooShort.Code);
    }

    [Theory]
    [InlineData("ftp://example.com/file")]
    [InlineData("not a url")]
    [InlineData("/relative")]
    public async Task InvalidUrl_IsRejected(string url)
    {
        var handler = Create(new InMemoryDocumentStore());
        var result = await handler.HandleAsync(new FetchPageCommand(Guid.NewGuid(), url, null), CancellationToken.None);
        result.Error!.Code.ShouldBe(FetchingErrors.InvalidUrl.Code);
    }

    private static DownloadedPage SamplePage(string url = "https://example.com/article") =>
        new(new Uri(url), "text/html", "utf-8", Encoding.UTF8.GetBytes("<p>body</p>"));

    private static FetchPageHandler Create(
        InMemoryDocumentStore store,
        bool robotsAllowed = true,
        FakePageDownloader? downloader = null,
        ExtractedContent? extracted = null,
        FetchingOptions? options = null) => new(
            new FakeRobotsPolicy(robotsAllowed),
            new FakeDomainThrottle(),
            downloader ?? new FakePageDownloader(Result<DownloadedPage>.Success(SamplePage())),
            new FakeContentExtractor(extracted ?? new ExtractedContent("Title", LongParagraph)),
            store,
            new FetchingMetrics(TestMeters.Create()),
            TestOptions.Of(options ?? new FetchingOptions { MinContentChars = 200 }),
            TestOptions.Of(new ContentLoggingOptions()),
            new FakeTimeProvider(),
            NullLoggerFactory.Instance);

    private sealed class FakeRobotsPolicy(bool allowed) : IRobotsPolicy
    {
        public Task<bool> IsAllowedAsync(Uri url, CancellationToken cancellationToken) => Task.FromResult(allowed);
    }

    private sealed class FakeDomainThrottle : IDomainThrottle
    {
        public Task<IDisposable> AcquireAsync(string host, CancellationToken cancellationToken) =>
            Task.FromResult<IDisposable>(NoOpDisposable.Instance);

        private sealed class NoOpDisposable : IDisposable
        {
            public static readonly NoOpDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed class FakePageDownloader(Result<DownloadedPage> result) : IPageDownloader
    {
        public int Calls { get; private set; }

        public Task<Result<DownloadedPage>> DownloadAsync(Uri url, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeContentExtractor(ExtractedContent content) : IContentExtractor
    {
        public ExtractedContent Extract(byte[] body, string mediaType, string? charset, string fallbackTitle) => content;
    }
}
