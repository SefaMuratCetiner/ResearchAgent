using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Fetching.Application.Common.Interfaces;
using Research.Fetching.Application.Observability;
using Research.Fetching.Domain;
using Research.Shared.Ports.Documents;
using Research.Shared.Core.Logging;
using Research.Shared.Core.Mediation;
using Research.Shared.Kernel.Security;
using Research.Shared.Kernel.Results;

namespace Research.Fetching.Application.FetchPage;

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
        if (!TryParseWebUrl(command.Url, out var url))
        {
            return Result<FetchedPage>.Failure(FetchingErrors.InvalidUrl);
        }

        var settings = _options.Value;
        var download = await DownloadAllowedAsync(url, settings, cancellationToken).ConfigureAwait(false);
        if (!download.IsSuccess)
        {
            return Result<FetchedPage>.Failure(download.Error);
        }

        var page = download.Value;
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

        if (ShouldRejectForInjection(command, text, settings))
        {
            return Result<FetchedPage>.Failure(FetchingErrors.SuspectedInjection);
        }

        var contentHash = Sha256Hex(text);
        var blobKey = $"pages/{contentHash}.txt";
        await _documents.SaveTextAsync(blobKey, text, cancellationToken).ConfigureAwait(false);

        if (_contentLogger.IsEnabled(LogLevel.Information))
        {
            var truncated = LogContentTruncator.Truncate(text, _contentOptions.Value.MaxChars);
            LogPageText(_contentLogger, command.JobId, command.Url, extracted.Title, truncated);
        }

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

    private bool ShouldRejectForInjection(FetchPageCommand command, string text, FetchingOptions settings)
    {
        var signals = PromptInjectionSignals.Find(text);
        if (signals.Count == 0)
        {
            return false;
        }

        _metrics.InjectionSignals(signals);
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            var summary = string.Join(", ", signals);
            LogInjectionSignals(_logger, command.JobId, command.Url, summary, settings.RejectSuspectedInjection);
        }

        return settings.RejectSuspectedInjection;
    }

    private static bool TryParseWebUrl(string urlText, out Uri url) =>
        Uri.TryCreate(urlText, UriKind.Absolute, out url!)
        && (string.Equals(url.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
            || string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal));

    private async Task<Result<DownloadedPage>> DownloadAllowedAsync(
        Uri url,
        FetchingOptions settings,
        CancellationToken cancellationToken)
    {
        if (settings.IsBlocked(url.IdnHost))
        {
            return Result<DownloadedPage>.Failure(FetchingErrors.BlockedDomain);
        }

        if (!await _robots.IsAllowedAsync(url, cancellationToken).ConfigureAwait(false))
        {
            return Result<DownloadedPage>.Failure(FetchingErrors.FetchDisallowed);
        }

        using (await _throttle.AcquireAsync(url.IdnHost, cancellationToken).ConfigureAwait(false))
        {
            return await _downloader.DownloadAsync(url, cancellationToken).ConfigureAwait(false);
        }
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
