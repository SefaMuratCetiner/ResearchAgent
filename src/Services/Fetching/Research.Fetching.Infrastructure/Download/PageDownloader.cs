using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Research.Fetching.Application.Common.Interfaces;
using Research.Fetching.Domain;
using Research.SharedKernel.Results;

namespace Research.Fetching.Infrastructure.Download;

/// <summary>
/// Downloads one page with the named <see cref="HttpClientName"/> client (SSRF-guarded connections, automatic
/// redirects up to the limit). Permanent outcomes (4xx, too large, redirect loop, blocked address, wrong type) are
/// failures; 5xx, 429 and network errors throw so the message is retried.
/// </summary>
public sealed class PageDownloader(IHttpClientFactory httpClientFactory, IOptionsMonitor<FetchingOptions> options) : IPageDownloader
{
    public const string HttpClientName = "fetching";

    public async Task<Result<DownloadedPage>> DownloadAsync(Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        var settings = options.CurrentValue;
        var client = httpClientFactory.CreateClient(HttpClientName);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.Timeout);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(settings.UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain", 0.8));

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        }
        catch (HttpRequestException exception) when (FindBlocked(exception) is not null)
        {
            return Result<DownloadedPage>.Failure(FetchingErrors.BlockedAddress);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<DownloadedPage>.Failure(FetchingErrors.Timeout);
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            if (status is >= 300 and < 400)
            {
                return Result<DownloadedPage>.Failure(FetchingErrors.RedirectLimit);
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests || status >= 500)
            {
                throw new HttpRequestException($"The site answered {status}; retrying later.", null, response.StatusCode);
            }

            if (status >= 400)
            {
                return Result<DownloadedPage>.Failure(FetchingErrors.HttpError);
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? "text/html";
            if (!settings.AllowedContentTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
            {
                return Result<DownloadedPage>.Failure(FetchingErrors.UnsupportedContent);
            }

            if (response.Content.Headers.ContentLength > settings.MaxBytes)
            {
                return Result<DownloadedPage>.Failure(FetchingErrors.TooLarge);
            }

            byte[]? body;
            try
            {
                body = await ReadLimitedAsync(response.Content, settings.MaxBytes, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return Result<DownloadedPage>.Failure(FetchingErrors.Timeout);
            }

            if (body is null)
            {
                return Result<DownloadedPage>.Failure(FetchingErrors.TooLarge);
            }

            return Result<DownloadedPage>.Success(new DownloadedPage(
                response.RequestMessage?.RequestUri ?? url,
                mediaType,
                response.Content.Headers.ContentType?.CharSet,
                body));
        }
    }

    /// <returns>The body, or null when it exceeds <paramref name="maxBytes"/>.</returns>
    private static async Task<byte[]?> ReadLimitedAsync(HttpContent content, int maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static BlockedAddressException? FindBlocked(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is BlockedAddressException blocked)
            {
                return blocked;
            }

            exception = exception.InnerException;
        }

        return null;
    }
}
