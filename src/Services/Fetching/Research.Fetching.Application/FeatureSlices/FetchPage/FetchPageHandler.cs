using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Fetching.Application.Common.Interfaces;
using Research.Fetching.Domain;
using Research.Shared.Core.Documents;
using Research.Shared.Core.Logging;
using Research.Shared.Core.Mediation;
using Research.Shared.Core.Security;
using Research.SharedKernel.Results;

namespace Research.Fetching.Application.FetchPage;

/// <summary>Fetch <see cref="Url"/> for a job; <see cref="Title"/> is the search result title, used when the page has none.</summary>
public sealed record FetchPageCommand(Guid JobId, string Url, string? Title) : ICommand<Result<FetchedPage>>;

/// <summary>A fetched page whose text is already in the document store under <see cref="BlobKey"/>.</summary>
public sealed record FetchedPage(
    string Url,
    string UrlHash,
    string FinalUrl,
    string Title,
    string MimeType,
    long SizeBytes,
    string ContentHash,
    int CharacterCount,
    string BlobKey);

/// <summary>
/// Validates the URL, checks robots.txt, downloads under the per-domain limit, extracts the text and stores it in the
/// document store under a content-addressed key (so a retry or another job fetching the same text writes the same
/// blob). The extracted text is logged under <see cref="ContentLogCategories.Documents"/>. No database work: the
/// message handler records the result in one transaction with the reply.
/// </summary>
public sealed partial class FetchPageHandler : ICommandHandler<FetchPageCommand, Result<FetchedPage>>
{
    private readonly IRobotsPolicy _robots;
    private readonly IDomainThrottle _throttle;
    private readonly IPageDownloader _downloader;
    private readonly IContentExtractor _extractor;
    private readonly IDocumentStore _documents;
    private readonly FetchingMetrics _metrics;
    private readonly IOptionsSnapshot<FetchingOptions> _options;
    private readonly IOptionsSnapshot<ContentLoggingOptions> _contentOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FetchPageHandler> _logger;
    private readonly ILogger _contentLogger;

    public FetchPageHandler(
        IRobotsPolicy robots,
        IDomainThrottle throttle,
        IPageDownloader downloader,
        IContentExtractor extractor,
        IDocumentStore documents,
        FetchingMetrics metrics,
        IOptionsSnapshot<FetchingOptions> options,
        IOptionsSnapshot<ContentLoggingOptions> contentOptions,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _robots = robots;
        _throttle = throttle;
        _downloader = downloader;
        _extractor = extractor;
        _documents = documents;
        _metrics = metrics;
        _options = options;
        _contentOptions = contentOptions;
        _timeProvider = timeProvider;
        _logger = loggerFactory.CreateLogger<FetchPageHandler>();
        _contentLogger = loggerFactory.CreateLogger(ContentLogCategories.Documents);
    }

    public async Task<Result<FetchedPage>> HandleAsync(FetchPageCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var started = _timeProvider.GetTimestamp();
        var result = await FetchAsync(command, cancellationToken).ConfigureAwait(false);
        var outcome = result.IsSuccess ? "ok" : result.Error.Code;
        _metrics.PageProcessed(outcome, _timeProvider.GetElapsedTime(started));

        if (result.IsSuccess)
        {
            LogFetched(_logger, command.JobId, command.Url, result.Value.CharacterCount, result.Value.SizeBytes);
        }
        else
        {
            LogRejected(_logger, command.JobId, command.Url, result.Error.Code);
        }

        return result;
    }

    private async Task<Result<FetchedPage>> FetchAsync(FetchPageCommand command, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(command.Url, UriKind.Absolute, out var url) || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            return Result<FetchedPage>.Failure(FetchingErrors.InvalidUrl);
        }

        var settings = _options.Value;
        if (settings.IsBlocked(url.IdnHost))
        {
            return Result<FetchedPage>.Failure(FetchingErrors.BlockedDomain);
        }

        if (!await _robots.IsAllowedAsync(url, cancellationToken).ConfigureAwait(false))
        {
            return Result<FetchedPage>.Failure(FetchingErrors.FetchDisallowed);
        }

        DownloadedPage page;
        using (await _throttle.AcquireAsync(url.IdnHost, cancellationToken).ConfigureAwait(false))
        {
            var download = await _downloader.DownloadAsync(url, cancellationToken).ConfigureAwait(false);
            if (!download.IsSuccess)
            {
                return Result<FetchedPage>.Failure(download.Error);
            }

            page = download.Value;
        }

        _metrics.PageSize(page.Body.LongLength);

        if (settings.IsBlocked(page.FinalUrl.IdnHost))
        {
            return Result<FetchedPage>.Failure(FetchingErrors.BlockedDomain);
        }

        var fallbackTitle = string.IsNullOrWhiteSpace(command.Title) ? url.Host : command.Title.Trim();
        var extracted = _extractor.Extract(page.Body, page.MediaType, page.Charset, fallbackTitle);
        var text = extracted.Text.Length > settings.MaxContentChars ? extracted.Text[..settings.MaxContentChars] : extracted.Text;
        if (text.Length < settings.MinContentChars)
        {
            return Result<FetchedPage>.Failure(FetchingErrors.ContentTooShort);
        }

        var signals = PromptInjectionSignals.Find(text);
        if (signals.Count > 0)
        {
            _metrics.InjectionSignals(signals);
            LogInjectionSignals(_logger, command.JobId, command.Url, string.Join(", ", signals), settings.RejectSuspectedInjection);
            if (settings.RejectSuspectedInjection)
            {
                return Result<FetchedPage>.Failure(FetchingErrors.SuspectedInjection);
            }
        }

        var contentHash = Sha256Hex(text);
        var blobKey = $"pages/{contentHash}.txt";
        await _documents.SaveTextAsync(blobKey, text, cancellationToken).ConfigureAwait(false);

        LogPageText(_contentLogger, command.JobId, command.Url, extracted.Title, ContentText.Truncate(text, _contentOptions.Value.MaxChars));

        return Result<FetchedPage>.Success(new FetchedPage(
            command.Url,
            Sha256Hex(command.Url),
            page.FinalUrl.AbsoluteUri,
            extracted.Title,
            page.MediaType,
            page.Body.LongLength,
            contentHash,
            text.Length,
            blobKey));
    }

    internal static string Sha256Hex(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    [LoggerMessage(EventId = 5000, Level = LogLevel.Information, Message = "Fetched {Url} for job {JobId}: {CharacterCount} characters from {SizeBytes} bytes")]
    private static partial void LogFetched(ILogger logger, Guid jobId, string url, int characterCount, long sizeBytes);

    [LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "Fetching {Url} for job {JobId} failed permanently: {ErrorCode}")]
    private static partial void LogRejected(ILogger logger, Guid jobId, string url, string errorCode);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Information, Message = "Page text of {Url} for job {JobId} ({Title}): {Text}")]
    private static partial void LogPageText(ILogger logger, Guid jobId, string url, string title, string text);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Warning, Message = "Page {Url} for job {JobId} shows prompt-injection signals {Signals} (rejected: {Rejected})")]
    private static partial void LogInjectionSignals(ILogger logger, Guid jobId, string url, string signals, bool rejected);
}
