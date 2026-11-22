namespace Research.Search.Domain.Policies;

/// <summary>
/// One spelling per page, so Jobs' per-URL step key deduplicates: http(s) only, lower-case host, default port and
/// fragment dropped, tracking parameters removed, trailing slash trimmed (except the root).
/// </summary>
public static class UrlCanonicalizer
{
    private static readonly string[] TrackingPrefixes = ["utm_", "mc_"];

    private static readonly HashSet<string> TrackingParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "fbclid", "gclid", "dclid", "msclkid", "yclid", "ref", "ref_src", "igshid", "_hsenc", "_hsmi",
    };

    /// <returns>The canonical URL, or null when the URL is not an absolute http(s) URL.</returns>
    public static string? Canonicalize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host))
        {
            return null;
        }

        var query = string.Join(
            '&',
            uri.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(static pair => !IsTracking(pair.Split('=', 2)[0])));

        var path = uri.AbsolutePath.Length > 1 ? uri.AbsolutePath.TrimEnd('/') : "/";
        var builder = new UriBuilder(uri.Scheme, uri.Host.ToLowerInvariant(), uri.IsDefaultPort ? -1 : uri.Port, path)
        {
            Query = query,
            Fragment = string.Empty,
        };

        return builder.Uri.AbsoluteUri;
    }

    private static bool IsTracking(string name) =>
        TrackingParameters.Contains(name)
        || TrackingPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
