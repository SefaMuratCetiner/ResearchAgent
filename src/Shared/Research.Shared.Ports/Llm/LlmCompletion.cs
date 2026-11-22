namespace Research.Shared.Ports.Llm;

/// <summary>A completion's text and accounting.</summary>
/// <param name="Text">All text blocks of the answer, concatenated.</param>
/// <param name="StopReason">Why the model stopped.</param>
/// <param name="Model">The model that actually answered (differs from the request after a refusal fallback).</param>
/// <param name="InputTokens">Billed input tokens.</param>
/// <param name="OutputTokens">Billed output tokens.</param>
public sealed record LlmCompletion(
    string Text,
    LlmStopReason StopReason,
    string Model,
    long InputTokens,
    long OutputTokens)
{
    public long TotalTokens => InputTokens + OutputTokens;
}

/// <summary>Why the model stopped.</summary>
public enum LlmStopReason
{
    Completed,
    MaxTokens,
    Refused,
    Other,
}
