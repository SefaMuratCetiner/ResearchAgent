using Microsoft.Extensions.Options;

namespace Research.Providers.Anthropic;

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
