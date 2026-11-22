using Microsoft.Extensions.Logging;
using Research.Knowledge.Application.Common.Interfaces;
using Research.Knowledge.Application.Observability;
using Research.Knowledge.Domain;
using Research.Shared.Core.Mediation;
using Research.Shared.Ports.Embeddings;
using Research.Shared.Kernel.Results;

namespace Research.Knowledge.Application.SearchPassages;

/// <summary>
/// Embeds every query, searches each one within the job's documents, merges the hits (a passage keeps its best
/// score) and returns the <c>TopK</c> best. A job with no indexed documents gets an empty list, not an error.
/// Bounds are enforced upstream by <see cref="SearchPassagesQueryValidator"/> via <c>ValidationBehavior</c>.
/// </summary>
public sealed partial class SearchPassagesHandler(
    IKnowledgeRepository repository,
    IEmbeddingGenerator embeddings,
    KnowledgeMetrics metrics,
    TimeProvider timeProvider,
    ILogger<SearchPassagesHandler> logger) : IQueryHandler<SearchPassagesQuery, Result<IReadOnlyList<PassageMatchDto>>>
{
    public async Task<Result<IReadOnlyList<PassageMatchDto>>> HandleAsync(SearchPassagesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var queries = SearchPassagesQuery.NormalizeQueries(query.Queries);

        var started = timeProvider.GetTimestamp();
        var vectors = await embeddings.GenerateAsync(queries, cancellationToken).ConfigureAwait(false);

        var best = new Dictionary<Guid, PassageMatch>();
        foreach (var vector in vectors)
        {
            var matches = await repository.SearchAsync(query.JobId, vector, embeddings.ModelId, query.TopK, cancellationToken)
                .ConfigureAwait(false);
            foreach (var match in matches)
            {
                if (!best.TryGetValue(match.PassageId, out var current) || match.Score > current.Score)
                {
                    best[match.PassageId] = match;
                }
            }
        }

        var result = best.Values
            .OrderByDescending(static m => m.Score)
            .Take(query.TopK)
            .Select(PassageMatchDto.From)
            .ToList();
        var elapsed = timeProvider.GetElapsedTime(started);
        metrics.SearchCompleted(elapsed);
        LogSearched(logger, query.JobId, queries.Count, result.Count, elapsed.TotalMilliseconds);
        return Result<IReadOnlyList<PassageMatchDto>>.Success(result);
    }

    [LoggerMessage(EventId = 6020, Level = LogLevel.Information, Message = "Passage search for job {JobId}: {QueryCount} queries, {ResultCount} passages in {ElapsedMs:F0} ms")]
    private static partial void LogSearched(ILogger logger, Guid jobId, int queryCount, int resultCount, double elapsedMs);
}
