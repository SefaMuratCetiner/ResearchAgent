using Microsoft.Extensions.Options;

namespace Research.Fetching;

/// <summary>Bound from the <c>Fetching</c> section.</summary>
public sealed class FetchingOptions
{
    public const string SectionName = "Fetching";

    /// <summary>Largest response body read; larger pages fail with <c>fetching.too_large</c>.</summary>
    public int MaxBytes { get; set; } = 5 * 1024 * 1024;

    public int MaxRedirects { get; set; } = 5;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>Extracted text shorter than this is not worth indexing (<c>fetching.content_too_short</c>).</summary>
    public int MinContentChars { get; set; } = 500;

    /// <summary>Extracted text longer than this is cut (very long pages add little after the first part).</summary>
    public int MaxContentChars { get; set; } = 200_000;

    public string UserAgent { get; set; } = "ResearchAgent/1.0 (+https://research.local/bot)";

    /// <summary>Concurrent downloads per host per replica.</summary>
    public int PerDomainConcurrency { get; set; } = 2;

    public bool RespectRobotsTxt { get; set; } = true;

    public TimeSpan RobotsCacheDuration { get; set; } = TimeSpan.FromHours(1);

    public string[] AllowedContentTypes { get; set; } = ["text/html", "application/xhtml+xml", "text/plain"];

    /// <summary>
    /// Hosts never fetched, with their subdomains (<c>example.com</c> also blocks <c>www.example.com</c>). Checked on
    /// the requested URL and on the final URL after redirects (<c>fetching.blocked_domain</c>).
    /// </summary>
    public string[] BlockedDomains { get; set; } = [];

    /// <summary>
    /// When true, a page whose text shows prompt-injection signals is not indexed (<c>fetching.suspected_injection</c>).
    /// Off by default: the signals are heuristics, and pages about prompt injection match them. The signals are always
    /// logged and counted either way.
    /// </summary>
    public bool RejectSuspectedInjection { get; set; }

    /// <summary>True when <paramref name="host"/> is a blocked domain or a subdomain of one.</summary>
    public bool IsBlocked(string host) =>
        BlockedDomains.Any(domain =>
            host.Equals(domain, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + domain.TrimStart('.'), StringComparison.OrdinalIgnoreCase));
}

/// <summary>Checks <see cref="FetchingOptions"/> at startup.</summary>
public sealed class FetchingOptionsValidator : IValidateOptions<FetchingOptions>
{
    public ValidateOptionsResult Validate(string? name, FetchingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (options.MaxBytes < 1024)
        {
            failures.Add("Fetching:MaxBytes must be at least 1024.");
        }

        if (options.MaxRedirects is < 0 or > 20)
        {
            failures.Add("Fetching:MaxRedirects must be between 0 and 20.");
        }

        if (options.Timeout <= TimeSpan.Zero)
        {
            failures.Add("Fetching:Timeout must be positive.");
        }

        if (options.MinContentChars < 0 || options.MaxContentChars < options.MinContentChars)
        {
            failures.Add("Fetching:MinContentChars must be non-negative and not above MaxContentChars.");
        }

        if (string.IsNullOrWhiteSpace(options.UserAgent))
        {
            failures.Add("Fetching:UserAgent is required.");
        }

        if (options.PerDomainConcurrency < 1)
        {
            failures.Add("Fetching:PerDomainConcurrency must be at least 1.");
        }

        if (options.AllowedContentTypes is not { Length: > 0 })
        {
            failures.Add("Fetching:AllowedContentTypes needs at least one type.");
        }

        if (options.BlockedDomains.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add("Fetching:BlockedDomains cannot contain empty entries.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
