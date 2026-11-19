using Microsoft.Extensions.Options;

namespace Research.Providers.Anthropic;

/// <summary>
/// Bound from the <c>Anthropic</c> section. <see cref="ApiKey"/> is a secret: it comes from the Aspire parameter
/// <c>anthropic-api-key</c> (user secrets locally) and this class is never logged whole.
/// </summary>
public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Concurrent requests per replica: the service's share of the account quota ÷ max replicas.</summary>
    public int MaxConcurrentRequests { get; set; } = 4;

    /// <summary>The model a refused request is retried on once. Empty disables the fallback.</summary>
    public string RefusalFallbackModel { get; set; } = "claude-sonnet-5";
}

/// <summary>Checks <see cref="AnthropicOptions"/> at startup. The failure message never contains the key.</summary>
public sealed class AnthropicOptionsValidator : IValidateOptions<AnthropicOptions>
{
    public ValidateOptionsResult Validate(string? name, AnthropicOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            failures.Add("Anthropic:ApiKey is required (set the anthropic-api-key parameter).");
        }

        if (options.MaxConcurrentRequests < 1)
        {
            failures.Add("Anthropic:MaxConcurrentRequests must be at least 1.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
