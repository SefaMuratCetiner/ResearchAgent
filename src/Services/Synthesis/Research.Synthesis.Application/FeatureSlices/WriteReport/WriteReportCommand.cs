using Research.Shared.Bus.Mediation;
using Research.Shared.Kernel.Results;

namespace Research.Synthesis.Application.WriteReport;

/// <summary>Write the report of a job from its indexed passages.</summary>
public sealed record WriteReportCommand(Guid JobId, string Question, IReadOnlyList<string> SubQuestions, int TopK)
    : ICommand<Result<WrittenReport>>, IJobScopedRequest;
