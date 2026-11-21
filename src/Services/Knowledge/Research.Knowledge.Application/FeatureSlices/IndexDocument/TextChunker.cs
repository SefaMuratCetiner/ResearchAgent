using System.Text;

namespace Research.Knowledge.Application.IndexDocument;

/// <summary>
/// Splits text into passages of about <c>chunkChars</c> characters. Paragraphs (blank-line separated, as Fetching
/// writes them) are packed whole; a paragraph longer than a chunk is cut at sentence or word boundaries. Each passage
/// after the first starts with the last <c>overlapChars</c> of the previous one (from a word boundary), so a fact that
/// straddles a boundary is still found.
/// </summary>
public static class TextChunker
{
    public static IReadOnlyList<string> Split(string text, int chunkChars, int overlapChars, int maxChunks)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkChars, 1);

        var pieces = text
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(paragraph => paragraph.Length <= chunkChars ? [paragraph] : SplitLong(paragraph, chunkChars));

        var chunks = new List<string>();
        var current = new StringBuilder();
        foreach (var piece in pieces)
        {
            if (current.Length > 0 && current.Length + 2 + piece.Length > chunkChars)
            {
                chunks.Add(current.ToString());
                if (chunks.Count >= maxChunks)
                {
                    return chunks;
                }

                var overlap = Tail(current.ToString(), overlapChars);
                current.Clear().Append(overlap);
            }

            if (current.Length > 0)
            {
                current.Append("\n\n");
            }

            current.Append(piece);
        }

        if (current.Length > 0 && chunks.Count < maxChunks)
        {
            chunks.Add(current.ToString());
        }

        return chunks;
    }

    /// <summary>Cuts a long paragraph at the last sentence end, else the last space, before the limit.</summary>
    private static IEnumerable<string> SplitLong(string paragraph, int chunkChars)
    {
        var start = 0;
        while (paragraph.Length - start > chunkChars)
        {
            var window = paragraph.AsSpan(start, chunkChars);
            var cut = LastSentenceEnd(window);
            if (cut < chunkChars / 2)
            {
                cut = window.LastIndexOf(' ') + 1;
            }

            if (cut <= 0)
            {
                cut = chunkChars;
            }

            yield return paragraph.Substring(start, cut).Trim();
            start += cut;
        }

        if (start < paragraph.Length)
        {
            yield return paragraph[start..].Trim();
        }
    }

    private static int LastSentenceEnd(ReadOnlySpan<char> window)
    {
        for (var i = window.Length - 2; i >= 0; i--)
        {
            if (window[i] is '.' or '!' or '?' && window[i + 1] == ' ')
            {
                return i + 2;
            }
        }

        return -1;
    }

    private static string Tail(string text, int overlapChars)
    {
        if (overlapChars <= 0 || text.Length <= overlapChars)
        {
            return overlapChars <= 0 ? string.Empty : text;
        }

        var tail = text[^overlapChars..];
        var space = tail.IndexOf(' ', StringComparison.Ordinal);
        return space >= 0 && space < tail.Length - 1 ? tail[(space + 1)..] : tail;
    }
}
