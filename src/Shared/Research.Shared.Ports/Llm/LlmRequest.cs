namespace Research.Shared.Ports.Llm;

/// <summary>A completion request.</summary>
/// <param name="Operation">A short name for logs, metrics and spans (<c>plan</c>, <c>synthesize</c>).</param>
/// <param name="Model">The model id, from the caller's options.</param>
/// <param name="Effort">How much effort the model spends.</param>
/// <param name="SystemPrompt">Instructions; cached by the provider when it supports caching.</param>
/// <param name="UserPrompt">The user turn.</param>
/// <param name="MaxTokens">The output token ceiling.</param>
/// <param name="JsonSchema">When set, the model must answer with JSON matching this schema (structured output).</param>
public sealed record LlmRequest(
    string Operation,
    string Model,
    LlmEffort Effort,
    string SystemPrompt,
    string UserPrompt,
    int MaxTokens,
    string? JsonSchema = null);
