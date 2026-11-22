using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Research.Fetching.Application.Common.Interfaces;
using Research.Shared.Kernel.Security;

namespace Research.Fetching.Infrastructure.Extraction;

/// <summary>
/// HTML to readable text with AngleSharp: drops scripts, styles, navigation, headers, footers, forms and asides,
/// prefers <c>&lt;article&gt;</c> or <c>&lt;main&gt;</c>, and keeps one paragraph per block element. The parser
/// detects the encoding from the BOM, the <c>Content-Type</c> charset or the meta tag.
/// <para>
/// Text a reader of the page cannot see is dropped, because it is the usual carrier of indirect prompt injection
/// (OWASP ASI01/ASI06): elements marked <c>hidden</c> or <c>aria-hidden</c>, elements hidden by an inline style
/// (<c>display:none</c>, <c>visibility:hidden</c>, zero font size or opacity, off-screen positioning), and invisible
/// Unicode characters (<see cref="UntrustedText.Clean"/>). Text hidden by a stylesheet is not detected.
/// </para>
/// </summary>
public sealed class ContentExtractor : IContentExtractor
{
    ExtractedContent IContentExtractor.Extract(byte[] body, string mediaType, string? charset, string fallbackTitle) =>
        Extract(body, mediaType, charset, fallbackTitle);
    private const string Noise = "script, style, noscript, template, svg, canvas, iframe, object, embed, nav, header, footer, aside, form, button, select, input, textarea, [hidden], [aria-hidden=true]";
    private const string Blocks = "h1, h2, h3, h4, h5, h6, p, li, pre, blockquote, td, th, dt, dd, figcaption";

    private static readonly string[] HiddenStyles =
    [
        "display:none", "visibility:hidden", "visibility:collapse", "font-size:0", "opacity:0", "max-height:0",
        "height:0;overflow:hidden", "clip:rect(0", "clip-path:inset(50%", "text-indent:-9", "left:-9", "top:-9",
    ];

    public static ExtractedContent Extract(byte[] body, string mediaType, string? charset, string fallbackTitle)
    {
        ArgumentNullException.ThrowIfNull(body);
        fallbackTitle = UntrustedText.SingleLine(fallbackTitle);
        if (mediaType.Equals("text/plain", StringComparison.OrdinalIgnoreCase))
        {
            var encoding = GetEncoding(charset);
            return new ExtractedContent(fallbackTitle, Normalize(UntrustedText.Clean(encoding.GetString(body))));
        }

        var parser = new HtmlParser();
        using var stream = new MemoryStream(body);
        using var document = parser.ParseDocument(stream);

        var title = UntrustedText.SingleLine(document.Title);
        foreach (var element in document.QuerySelectorAll(Noise).ToList())
        {
            element.Remove();
        }

        foreach (var element in document.QuerySelectorAll("[style]").Where(IsHiddenByStyle).ToList())
        {
            element.Remove();
        }

        var root = document.QuerySelector("article") ?? document.QuerySelector("main") ?? document.Body;
        if (root is null)
        {
            return new ExtractedContent(string.IsNullOrWhiteSpace(title) ? fallbackTitle : title, string.Empty);
        }

        var text = new StringBuilder();
        string? previous = null;
        foreach (var block in root.QuerySelectorAll(Blocks))
        {
            // Nested blocks (a <p> inside an <li>) would repeat text; keep only innermost blocks.
            if (block.QuerySelector(Blocks) is not null)
            {
                continue;
            }

            var paragraph = Normalize(UntrustedText.Clean(block.TextContent));
            if (paragraph.Length == 0 || string.Equals(paragraph, previous, StringComparison.Ordinal))
            {
                continue;
            }

            text.Append(paragraph).Append("\n\n");
            previous = paragraph;
        }

        var result = text.Length > 0 ? text.ToString().Trim() : Normalize(UntrustedText.Clean(root.TextContent));
        return new ExtractedContent(string.IsNullOrWhiteSpace(title) ? fallbackTitle : title, result);
    }

    /// <summary>True when the inline style hides the element (whitespace and case ignored).</summary>
    internal static bool IsHiddenByStyle(IElement element)
    {
        var style = element.GetAttribute("style");
        if (string.IsNullOrEmpty(style))
        {
            return false;
        }

        var compact = string.Concat(style.Where(static c => !char.IsWhiteSpace(c))).ToLowerInvariant().Replace("!important", string.Empty, StringComparison.Ordinal);
        return HiddenStyles.Any(hidden => compact.Contains(hidden, StringComparison.Ordinal)
            && !IsNonZeroAfter(compact, hidden));
    }

    // "font-size:0" also matches "font-size:0.9em"; "opacity:0" matches "opacity:0.8". Those are visible.
    private static bool IsNonZeroAfter(string style, string hidden)
    {
        if (!hidden.EndsWith('0'))
        {
            return false;
        }

        var index = style.IndexOf(hidden, StringComparison.Ordinal) + hidden.Length;
        return index < style.Length && (style[index] == '.' || char.IsDigit(style[index]))
            && style[index..].TakeWhile(static c => c == '.' || char.IsDigit(c)).Any(static c => c is >= '1' and <= '9');
    }

    private static Encoding GetEncoding(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charset.Trim('"'));
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                space = builder.Length > 0;
                continue;
            }

            if (space)
            {
                builder.Append(' ');
                space = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }
}
