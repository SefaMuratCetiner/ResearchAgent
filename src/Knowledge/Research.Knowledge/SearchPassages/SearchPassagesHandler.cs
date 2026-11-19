using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Knowledge.Domain;
using Research.SharedKernel.Embeddings;
using Research.SharedKernel.Results;

namespace Research.Knowledge.SearchPassages;

/// <summary>The passages of <see cref="JobId"/>'s documents most similar to any of <see cref="Queries"/>.</summary>
public sealed record SearchPassagesQuery(Guid JobId, IReadOnlyList<string> Queries, int TopK);

/// <summary>
/// Embeds every query, searches each one within the job's documents, merges the hits (a passage keeps its best
/// score) and returns the <c>TopK</c> best. A job with no indexed documents gets an empty list, not an error.
/// </summary>
public sealed partial class SearchPassagesHandler(
    IKnowledgeRepository repository,
    IEmbeddingGenerator embeddings,
    KnowledgeMetrics metrics,
    IOptionsSnapshot<KnowledgeOptions> options,
    TimeProvider timeProvider,
    ILogger<SearchPassagesHandler> logger)
{
    public async Task<Result<IReadOnlyList<PassageMatch>>> HandleAsync(SearchPassagesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var settings = options.Value;
        var queries = (query.Queries ?? [])
            .Select(static q => q?.Trim() ?? string.Empty)
            .Where(static q => q.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (query.JobId == Guid.Empty
            || queries.Count == 0
            || queries.Count > settings.MaxQueries
            || queries.Any(q => q.Length > settings.MaxQueryLength)
            || query.TopK < 1
            || query.TopK > settings.MaxTopK)
        {
            return Result<IReadOnlyList<PassageMatch>>.Failure(KnowledgeErrors.InvalidSearch);
        }

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

        var result = best.Values.OrderByDescending(static m => m.Score).Take(query.TopK).ToList();
        var elapsed = timeProvider.GetElapsedTime(started);
        metrics.SearchCompleted(elapsed);
        LogSearched(logger, query.JobId, queries.Count, result.Count, elapsed.TotalMilliseconds);
        return Result<IReadOnlyList<PassageMatch>>.Success(result);
    }

    [LoggerMessage(EventId = 6020, Level = LogLevel.Information, Message = "Passage search for job {JobId}: {QueryCount} queries, {ResultCount} passages in {ElapsedMs:F0} ms")]
    private static partial void LogSearched(ILogger logger, Guid jobId, int queryCount, int resultCount, double elapsedMs);
}
