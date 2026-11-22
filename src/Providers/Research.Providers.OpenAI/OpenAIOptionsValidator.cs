using Microsoft.Extensions.Options;

namespace Research.Providers.OpenAI;

/// <summary>Checks <see cref="OpenAIOptions"/> at startup. The failure message never contains the key.</summary>
public sealed class OpenAIOptionsValidator : IValidateOptions<OpenAIOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenAIOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            failures.Add("OpenAI:ApiKey is required (set the openai-api-key parameter).");
        }

        if (options.MaxConcurrentRequests < 1)
        {
            failures.Add("OpenAI:MaxConcurrentRequests must be at least 1.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
