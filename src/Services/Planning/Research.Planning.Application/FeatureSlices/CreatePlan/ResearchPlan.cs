namespace Research.Planning.Application.CreatePlan;

/// <summary>The plan and what it cost; a cached plan costs nothing.</summary>
public sealed record ResearchPlan(IReadOnlyList<string> SubQuestions, long InputTokens, long OutputTokens, bool FromCache);
