using Microsoft.Extensions.Options;
using Research.SharedKernel.Llm;

namespace Research.Planning;

/// <summary>Bound from the <c>Planning</c> section.</summary>
public sealed class PlanningOptions
{
    public const string SectionName = "Planning";

    public string Model { get; set; } = "claude-sonnet-5-5";

    /// <summary>Set explicitly: the model's own default is higher.</summary>
    public LlmEffort Effort { get; set; } = LlmEffort.Medium;

    public int MaxTokens { get; set; } = 4000;

    /// <summary>How long a plan stays cached; zero disables the cache.</summary>
    public TimeSpan CacheTimeToLive { get; set; } = TimeSpan.FromHours(24);
}

/// <summary>Checks <see cref="PlanningOptions"/> at startup.</summary>
public sealed class PlanningOptionsValidator : IValidateOptions<PlanningOptions>
{
    public ValidateOptionsResult Validate(string? name, PlanningOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Model))
        {
            failures.Add("Planning:Model is required.");
        }

        if (options.MaxTokens < 256)
        {
            failures.Add("Planning:MaxTokens must be at least 256.");
        }

        if (options.CacheTimeToLive < TimeSpan.Zero)
        {
            failures.Add("Planning:CacheTimeToLive cannot be negative.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
