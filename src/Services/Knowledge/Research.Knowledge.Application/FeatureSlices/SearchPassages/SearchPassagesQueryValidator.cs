using Microsoft.Extensions.Options;
using Research.Shared.Bus.Mediation;
using Research.Shared.Kernel.Results;

namespace Research.Knowledge.Application.SearchPassages;

/// <summary>Validates <see cref="SearchPassagesQuery"/> bounds before <see cref="SearchPassagesHandler"/> runs.</summary>
public sealed class SearchPassagesQueryValidator(IOptionsSnapshot<KnowledgeOptions> options)
    : IRequestValidator<SearchPassagesQuery>
{
    public ValueTask<Error?> ValidateAsync(SearchPassagesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = options.Value;
        var queries = SearchPassagesQuery.NormalizeQueries(request.Queries);

        var invalid = request.JobId == Guid.Empty
            || queries.Count == 0
            || queries.Count > settings.MaxQueries
            || queries.Any(q => q.Length > settings.MaxQueryLength)
            || request.TopK < 1
            || request.TopK > settings.MaxTopK;

        return ValueTask.FromResult(invalid ? KnowledgeErrors.InvalidSearch : null);
    }
}
