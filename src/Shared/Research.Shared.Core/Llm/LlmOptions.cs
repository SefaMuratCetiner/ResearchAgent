namespace Research.Shared.Core.Llm;

/// <summary>
/// Bound from the <c>Llm</c> section. Read once at composition time: changing <see cref="Provider"/> needs a restart.
/// The selected provider's own section (<c>Anthropic</c> or <c>OpenAI</c>) holds its key and limits, and the module's
/// <c>Model</c> must name a model of that provider.
/// </summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public LlmProvider Provider { get; set; } = LlmProvider.Anthropic;
}

/// <summary>The vendor behind <see cref="ILlmClient"/>.</summary>
public enum LlmProvider
{
    Anthropic,
    OpenAI,
}
