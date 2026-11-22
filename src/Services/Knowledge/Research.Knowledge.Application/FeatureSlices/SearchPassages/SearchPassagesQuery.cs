using Research.Shared.Core.Mediation;
using Research.SharedKernel.Results;

namespace Research.Knowledge.Application.SearchPassages;

/// <summary>The passages of <see cref="JobId"/>'s documents most similar to any of <see cref="Queries"/>.</summary>
public sealed record SearchPassagesQuery(Guid JobId, IReadOnlyList<string> Queries, int TopK)
    : IQuery<Result<IReadOnlyList<PassageMatchDto>>>
{
    internal static List<string> NormalizeQueries(IReadOnlyList<string>? queries) =>
        (queries ?? [])
            .Select(static q => q?.Trim() ?? string.Empty)
            .Where(static q => q.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
