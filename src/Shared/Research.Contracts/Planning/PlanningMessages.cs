namespace Research.Contracts.Planning;

/// <summary>Jobs asks Planning to split the question into sub-questions.</summary>
public sealed record PlanRequested(Guid JobId, string StepKey, string Question, int MaxSubQuestions) : IIntegrationMessage
{
    public static string MessageType => "planning.plan-requested";

    public static MessageKind Kind => MessageKind.Command;
}

/// <summary>Planning's answer: the sub-questions to search for, and what the plan cost.</summary>
public sealed record PlanReady(
    Guid JobId,
    string StepKey,
    IReadOnlyList<string> SubQuestions,
    long InputTokens,
    long OutputTokens) : IIntegrationMessage
{
    public static string MessageType => "planning.plan-ready";

    public static MessageKind Kind => MessageKind.Event;
}
