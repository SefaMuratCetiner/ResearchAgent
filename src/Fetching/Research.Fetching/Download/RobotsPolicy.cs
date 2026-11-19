using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Options;

namespace Research.Fetching.Download;

/// <summary>
/// A small robots.txt reader: the rules of the <c>User-agent: *</c> group (or a group naming our agent), longest
/// matching <c>Allow</c>/<c>Disallow</c> prefix wins. Rules are cached per host in memory. A missing robots.txt allows
/// everything; an unreachable one is treated as allowing (the fetch itself will then fail or succeed on its own).
/// </summary>
public sealed class RobotsPolicy(IHttpClientFactory httpClientFactory, IOptionsMonitor<FetchingOptions> options, TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, CachedRules> _cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<bool> IsAllowedAsync(Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        var settings = options.CurrentValue;
        if (!settings.RespectRobotsTxt)
        {
            return true;
        }

        var origin = url.GetLeftPart(UriPartial.Authority);
        var now = timeProvider.GetUtcNow();
        if (!_cache.TryGetValue(origin, out var cached) || cached.ExpiresAt <= now)
        {
            var rules = await LoadAsync(new Uri(origin + "/robots.txt"), settings, cancellationToken).ConfigureAwait(false);
            cached = new CachedRules(rules, now + settings.RobotsCacheDuration);
            _cache[origin] = cached;
        }

        return cached.Rules.IsAllowed(url.PathAndQuery);
    }

    private async Task<RobotsRules> LoadAsync(Uri robotsUrl, FetchingOptions settings, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(PageDownloader.HttpClientName);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            using var request = new HttpRequestMessage(HttpMethod.Get, robotsUrl);
            request.Headers.UserAgent.ParseAdd(settings.UserAgent);
            using var response = await client.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return RobotsRules.DisallowAll;
            }

            if (!response.IsSuccessStatusCode)
            {
                return RobotsRules.AllowAll;
            }

            var text = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            return RobotsRules.Parse(text, AgentToken(settings.UserAgent));
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or BlockedAddressException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return RobotsRules.AllowAll;
        }
    }

    private static string AgentToken(string userAgent) => userAgent.Split('/', ' ')[0];

    private sealed record CachedRules(RobotsRules Rules, DateTimeOffset ExpiresAt);
}

/// <summary>Parsed robots.txt rules for one agent.</summary>
public sealed class RobotsRules
{
    private readonly List<(string Prefix, bool Allow)> _rules;

    private RobotsRules(List<(string Prefix, bool Allow)> rules) => _rules = rules;

    public static RobotsRules AllowAll { get; } = new([]);

    public static RobotsRules DisallowAll { get; } = new([("/", false)]);

    public static RobotsRules Parse(string text, string agent)
    {
        ArgumentNullException.ThrowIfNull(text);
        var specific = new List<(string, bool)>();
        var wildcard = new List<(string, bool)>();
        List<List<(string, bool)>> currentTargets = [];
        var lastWasAgent = false;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Split('#', 2)[0].Trim();
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                continue;
            }

            var field = line[..colon].Trim().ToUpperInvariant();
            var value = line[(colon + 1)..].Trim();

            if (field == "USER-AGENT")
            {
                if (!lastWasAgent)
                {
                    currentTargets = [];
                }

                if (value == "*")
                {
                    currentTargets.Add(wildcard);
                }
                else if (value.Equals(agent, StringComparison.OrdinalIgnoreCase))
                {
                    currentTargets.Add(specific);
                }

                lastWasAgent = true;
                continue;
            }

            lastWasAgent = false;
            if ((field == "ALLOW" || field == "DISALLOW") && value.Length > 0)
            {
                foreach (var target in currentTargets)
                {
                    target.Add((value, field == "ALLOW"));
                }
            }
        }

        return new RobotsRules(specific.Count > 0 ? specific : wildcard);
    }

    public bool IsAllowed(string pathAndQuery)
    {
        var best = -1;
        var allowed = true;
        foreach (var (prefix, allow) in _rules)
        {
            var pattern = prefix.TrimEnd('*');
            if (pathAndQuery.StartsWith(pattern, StringComparison.Ordinal) && pattern.Length > best)
            {
                best = pattern.Length;
                allowed = allow;
            }
        }

        return allowed;
    }
}
