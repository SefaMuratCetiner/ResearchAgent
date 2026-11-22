using Research.Shared.Core.Mediation;
using Research.SharedKernel.Results;

namespace Research.Planning.Application.CreatePlan;

/// <summary>Split <see cref="Question"/> into at most <see cref="MaxSubQuestions"/> search sub-questions.</summary>
public sealed record CreatePlanCommand(string Question, int MaxSubQuestions) : ICommand<Result<ResearchPlan>>;

/// <summary>The plan and what it cost; a cached plan costs nothing.</summary>
public sealed record ResearchPlan(IReadOnlyList<string> SubQuestions, long InputTokens, long OutputTokens, bool FromCache);
