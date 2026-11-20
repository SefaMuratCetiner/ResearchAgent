using System.Diagnostics;

namespace Research.Shared.Adapters.Llm;

/// <summary>One client span per model call, whichever provider makes it, tagged with <c>gen_ai.*</c> attributes.</summary>
public static class LlmTracing
{
    public const string SourceName = "Research.Llm";

    public static ActivitySource Source { get; } = new(SourceName);
}
