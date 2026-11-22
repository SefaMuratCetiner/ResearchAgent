namespace Research.Providers.AnthropicWebSearch;

/// <summary>
/// Bound from the <c>AnthropicWebSearch</c> section. <see cref="ApiKey"/> is a secret (Aspire parameter
/// <c>anthropic-api-key</c>) and this class is never logged whole.
/// </summary>
public sealed class AnthropicWebSearchOptions
{
    public const string SectionName = "AnthropicWebSearch";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "claude-sonnet-5-5";

    /// <summary>Web searches Claude may run per sub-question.</summary>
    public int MaxUses { get; set; } = 3;

    public int MaxTokens { get; set; } = 4000;

    /// <summary>Only these domains, when set.</summary>
    public string[] AllowedDomains { get; set; } = [];

    /// <summary>Never these domains.</summary>
    public string[] BlockedDomains { get; set; } = [];
}
