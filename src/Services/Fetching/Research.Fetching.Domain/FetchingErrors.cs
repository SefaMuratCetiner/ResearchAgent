using Research.Shared.Kernel.Results;

namespace Research.Fetching.Domain;

/// <summary>
/// Every error code the Fetching service owns: permanent reasons a page cannot be used. Transient failures (5xx,
/// connection resets, timeouts) are retried and end as <c>research.step_exhausted</c>.
/// </summary>
public static class FetchingErrors
{
    public static readonly Error FetchDisallowed = new("fetching.fetch_disallowed", ErrorKind.Forbidden, "robots.txt disallows fetching the page.");

    public static readonly Error BlockedAddress = new("fetching.blocked_address", ErrorKind.Forbidden, "The page resolves to a private or reserved address.");

    public static readonly Error InvalidUrl = new("fetching.invalid_url", ErrorKind.Invalid, "The URL is not an absolute http(s) URL.");

    public static readonly Error UnsupportedContent = new("fetching.unsupported_content", ErrorKind.Rule, "The page is not HTML or plain text.");

    public static readonly Error TooLarge = new("fetching.too_large", ErrorKind.Rule, "The page is larger than the size limit.");

    public static readonly Error ContentTooShort = new("fetching.content_too_short", ErrorKind.Rule, "The page has too little readable text.");

    public static readonly Error RedirectLimit = new("fetching.redirect_limit", ErrorKind.Rule, "The page redirects too many times.");

    public static readonly Error HttpError = new("fetching.http_error", ErrorKind.Rule, "The site answered with a client error (4xx).");

    public static readonly Error Timeout = new("fetching.timeout", ErrorKind.Rule, "The site did not answer in time.");

    public static readonly Error BlockedDomain = new("fetching.blocked_domain", ErrorKind.Forbidden, "The page is on a blocked domain.");

    public static readonly Error SuspectedInjection = new("fetching.suspected_injection", ErrorKind.Rule, "The page text looks written to steer the model, and such pages are rejected.");

    public static readonly ErrorSet All = new(
        "fetching.",
        [
            FetchDisallowed,
            BlockedAddress,
            InvalidUrl,
            UnsupportedContent,
            TooLarge,
            ContentTooShort,
            RedirectLimit,
            HttpError,
            Timeout,
            BlockedDomain,
            SuspectedInjection,
        ]);
}
