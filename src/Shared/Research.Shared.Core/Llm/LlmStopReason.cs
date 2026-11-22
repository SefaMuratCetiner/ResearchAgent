namespace Research.Shared.Core.Llm;

/// <summary>Why the model stopped.</summary>
public enum LlmStopReason
{
    Completed,
    MaxTokens,
    Refused,
    Other,
}
