using Microsoft.Extensions.Options;

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

/// <summary>Checks <see cref="AnthropicWebSearchOptions"/> at startup. Failure messages never contain the key.</summary>
public sealed class AnthropicWebSearchOptionsValidator : IValidateOptions<AnthropicWebSearchOptions>
{
    public ValidateOptionsResult Validate(string? name, AnthropicWebSearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            failures.Add("AnthropicWebSearch:ApiKey is required (set the anthropic-api-key parameter).");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            failures.Add("AnthropicWebSearch:Model is required.");
        }

        if (options.MaxUses is < 1 or > 10)
        {
            failures.Add("AnthropicWebSearch:MaxUses must be between 1 and 10.");
        }

        if (options.MaxTokens < 512)
        {
            failures.Add("AnthropicWebSearch:MaxTokens must be at least 512.");
        }

        if (options.AllowedDomains.Length > 0 && options.BlockedDomains.Length > 0)
        {
            failures.Add("AnthropicWebSearch: set AllowedDomains or BlockedDomains, not both.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
