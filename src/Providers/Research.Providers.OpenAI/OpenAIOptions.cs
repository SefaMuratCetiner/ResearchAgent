using Microsoft.Extensions.Options;

namespace Research.Providers.OpenAI;

/// <summary>
/// Bound from the <c>OpenAI</c> section. <see cref="ApiKey"/> is a secret: it comes from the Aspire parameter
/// <c>openai-api-key</c> (user secrets locally) and this class is never logged whole.
/// </summary>
public sealed class OpenAIOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Concurrent requests per replica: the service's share of the account quota ÷ max replicas.</summary>
    public int MaxConcurrentRequests { get; set; } = 4;

    /// <summary>The model a refused request is retried on once. Empty disables the fallback.</summary>
    public string RefusalFallbackModel { get; set; } = string.Empty;

    /// <summary>
    /// Sends the request's effort as <c>reasoning_effort</c>. Turn it off for models without reasoning (the GPT-4
    /// family), which reject the parameter.
    /// </summary>
    public bool UseReasoningEffort { get; set; } = true;
}

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
