using Microsoft.Extensions.Options;
using Research.Shared.Ports.Llm;

namespace Research.Synthesis.Application;

/// <summary>Bound from the <c>Synthesis</c> section.</summary>
public sealed class SynthesisOptions
{
    public const string SectionName = "Synthesis";

    public string Model { get; set; } = "claude-sonnet-5-5";

    /// <summary>Set explicitly: the model's own default is higher.</summary>
    public LlmEffort Effort { get; set; } = LlmEffort.Medium;

    /// <summary>Output ceiling; non-streaming request, so kept well within the API's synchronous limits.</summary>
    public int MaxTokens { get; set; } = 16_000;

    /// <summary>Passage text sent to the model; lower-ranked passages beyond it are dropped.</summary>
    public int MaxContextChars { get; set; } = 120_000;

    /// <summary>The Knowledge API (Aspire service discovery name).</summary>
    public Uri KnowledgeBaseAddress { get; set; } = new("https+http://knowledge");
}

/// <summary>Checks <see cref="SynthesisOptions"/> at startup.</summary>
public sealed class SynthesisOptionsValidator : IValidateOptions<SynthesisOptions>
{
    public ValidateOptionsResult Validate(string? name, SynthesisOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Model))
        {
            failures.Add("Synthesis:Model is required.");
        }

        if (options.MaxTokens is < 1024 or > 64_000)
        {
            failures.Add("Synthesis:MaxTokens must be between 1024 and 64000.");
        }

        if (options.MaxContextChars < 1000)
        {
            failures.Add("Synthesis:MaxContextChars must be at least 1000.");
        }

        if (options.KnowledgeBaseAddress is null)
        {
            failures.Add("Synthesis:KnowledgeBaseAddress is required.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
