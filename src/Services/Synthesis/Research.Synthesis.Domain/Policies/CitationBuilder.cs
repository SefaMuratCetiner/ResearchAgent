using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Research.SharedKernel.Security;
using Research.Synthesis.Domain;

namespace Research.Synthesis.Domain.Policies;

/// <summary>
/// Numbers sources (one number per URL, in rank order), renders the numbered context the model cites from, and
/// afterwards keeps only the sources the report actually cites. Every excerpt is fenced (<see cref="PromptFence"/>),
/// because it is web text that may try to steer the model (OWASP ASI01, LLM01).
/// </summary>
public sealed partial class CitationBuilder
{
    private readonly Dictionary<string, Source> _sources = new(StringComparer.Ordinal);

    public sealed record Source(int Number, string Url, string Title);

    /// <summary>Every numbered source so far, by number.</summary>
    public IReadOnlyList<Source> Sources => _sources.Values.OrderBy(static s => s.Number).ToList();

    /// <summary>
    /// Renders passages as fenced <c>source</c> elements carrying their number, title and URL, stopping at
    /// <paramref name="maxChars"/> (at least one passage is always kept). Passages without a web URL are skipped.
    /// </summary>
    public string BuildContext(IReadOnlyList<RetrievedPassage> passages, int maxChars, PromptFence fence)
    {
        ArgumentNullException.ThrowIfNull(passages);
        ArgumentNullException.ThrowIfNull(fence);
        var builder = new StringBuilder();
        foreach (var passage in passages)
        {
            if (!UntrustedText.IsWebUrl(passage.Url))
            {
                continue;
            }

            var source = SourceFor(passage);
            var block = fence.Wrap(
                "source",
                passage.Text,
                ("n", PromptFence.Number(source.Number)),
                ("title", source.Title),
                ("url", source.Url)) + "\n\n";
            if (builder.Length + block.Length > maxChars && builder.Length > 0)
            {
                break;
            }

            builder.Append(block);
        }

        return builder.ToString();
    }

    /// <summary>The sources whose <c>[n]</c> marker appears in <paramref name="report"/>, by number.</summary>
    public IReadOnlyList<Source> CitedIn(string report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var cited = CitationMarker().Matches(report)
            .Select(static m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToHashSet();
        return _sources.Values.Where(s => cited.Contains(s.Number)).OrderBy(static s => s.Number).ToList();
    }

    private Source SourceFor(RetrievedPassage passage)
    {
        if (!_sources.TryGetValue(passage.Url, out var source))
        {
            var title = UntrustedText.SingleLine(passage.Title);
            source = new Source(_sources.Count + 1, passage.Url, title.Length == 0 ? passage.Url : title);
            _sources[passage.Url] = source;
        }

        return source;
    }

    [GeneratedRegex(@"\[(\d{1,3})\](?!\()", RegexOptions.CultureInvariant)]
    internal static partial Regex CitationMarker();
}
