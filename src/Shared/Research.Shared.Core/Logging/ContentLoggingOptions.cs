namespace Research.Shared.Core.Logging;

/// <summary>Bound from the <c>ContentLogging</c> section.</summary>
public sealed class ContentLoggingOptions
{
    public const string SectionName = "ContentLogging";

    /// <summary>The longest content value written to one log event; longer values are cut and marked.</summary>
    public int MaxChars { get; set; } = 64_000;
}
