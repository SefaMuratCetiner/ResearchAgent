using System.Text;

namespace Research.Shared.Core.Security;

/// <summary>
/// Cleans text that came from outside the system (web pages, search results, the user's question, model output)
/// before it reaches a prompt, a log or a message (OWASP ASI01/ASI06, LLM01). It removes characters a person cannot
/// see but a model reads: the Unicode tag block ("ASCII smuggling"), variation selectors, bidirectional overrides,
/// zero-width characters and control characters. It does not try to detect instructions: see
/// <see cref="PromptInjectionSignals"/> for that, and <see cref="PromptFence"/> for how untrusted text enters a prompt.
/// </summary>
public static class UntrustedText
{
    /// <summary>Longest title, query or sub-question kept on one line.</summary>
    public const int DefaultLineChars = 300;

    /// <summary>Removes invisible and control characters; keeps line breaks (as <c>\n</c>) and tabs.</summary>
    public static string Clean(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var previousWasCarriageReturn = false;
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Value == '\r')
            {
                builder.Append('\n');
                previousWasCarriageReturn = true;
                continue;
            }

            if (rune.Value == '\n' && previousWasCarriageReturn)
            {
                previousWasCarriageReturn = false;
                continue;
            }

            previousWasCarriageReturn = false;
            if (IsHidden(rune))
            {
                continue;
            }

            builder.Append(rune.ToString());
        }

        return builder.ToString();
    }

    /// <summary>
    /// <see cref="Clean"/>, then one line: every run of whitespace becomes one space, the result is trimmed and cut at
    /// <paramref name="maxChars"/>. For titles, queries and sub-questions, so they cannot open a new prompt section.
    /// </summary>
    public static string SingleLine(string? value, int maxChars = DefaultLineChars)
    {
        var cleaned = Clean(value);
        var builder = new StringBuilder(Math.Min(cleaned.Length, maxChars));
        var space = false;
        foreach (var c in cleaned)
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
            if (builder.Length >= maxChars)
            {
                break;
            }
        }

        return builder.ToString().Trim();
    }

    /// <summary>True for an absolute http(s) URL no longer than <paramref name="maxChars"/>.</summary>
    public static bool IsWebUrl(string? value, int maxChars = 2048) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= maxChars
        && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrEmpty(uri.Host);

    private static bool IsHidden(Rune rune)
    {
        var value = rune.Value;
        if (value is 0x0A or 0x09)
        {
            return false;
        }

        return Rune.GetUnicodeCategory(rune) == System.Globalization.UnicodeCategory.Control
            || value is 0x00AD or 0x034F or 0x061C or 0x115F or 0x1160 or 0x17B4 or 0x17B5 or 0x180E or 0x3164 or 0xFEFF or 0xFFA0
            || value is >= 0x200B and <= 0x200F // zero-width space/joiners, LRM, RLM
            || value is >= 0x202A and <= 0x202E // bidi embeddings and overrides
            || value is >= 0x2060 and <= 0x206F // word joiner, invisible operators, bidi isolates, deprecated formats
            || value is >= 0xFE00 and <= 0xFE0F // variation selectors
            || value is >= 0xFFF9 and <= 0xFFFB // interlinear annotations
            || value is >= 0xE0000 and <= 0xE007F // tag characters
            || value is >= 0xE0100 and <= 0xE01EF; // variation selectors supplement
    }
}
