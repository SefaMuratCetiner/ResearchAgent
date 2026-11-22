using Research.Shared.Bus.Mediation;
using Research.Shared.Kernel.Results;

namespace Research.Planning.Application.CreatePlan;

/// <summary>Split <see cref="Question"/> into at most <see cref="MaxSubQuestions"/> search sub-questions.</summary>
public sealed record CreatePlanCommand(string Question, int MaxSubQuestions) : ICommand<Result<ResearchPlan>>;
