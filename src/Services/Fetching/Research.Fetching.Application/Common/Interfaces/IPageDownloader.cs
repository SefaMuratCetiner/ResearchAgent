using Research.SharedKernel.Results;

namespace Research.Fetching.Application.Common.Interfaces;

/// <summary>The bytes of a page and how the site described them.</summary>
public sealed record DownloadedPage(Uri FinalUrl, string MediaType, string? Charset, byte[] Body);

/// <summary>Downloads a web page safely (SSRF checks, size and redirect limits).</summary>
public interface IPageDownloader
{
    Task<Result<DownloadedPage>> DownloadAsync(Uri url, CancellationToken cancellationToken);
}
