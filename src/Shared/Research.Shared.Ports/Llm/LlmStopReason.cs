namespace Research.Shared.Ports.Llm;

/// <summary>Why the model stopped.</summary>
public enum LlmStopReason
{
    Completed,
    MaxTokens,
    Refused,
    Other,
}
