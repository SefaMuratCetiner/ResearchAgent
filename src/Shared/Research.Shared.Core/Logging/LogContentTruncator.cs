using System.Globalization;

namespace Research.Shared.Core.Logging;

/// <summary>Keeps a content value within the log size cap and says so when it was cut.</summary>
public static class LogContentTruncator
{
    public static string Truncate(string? value, int maxChars)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (maxChars <= 0 || value.Length <= maxChars)
        {
            return value;
        }

        return string.Concat(
            value.AsSpan(0, maxChars),
            string.Create(CultureInfo.InvariantCulture, $"…[truncated {value.Length - maxChars} chars]"));
    }
}
