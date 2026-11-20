using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Research.Knowledge.SearchPassages;
using Research.ServiceDefaults.Middleware;
using Research.ServiceDefaults.Problems;

namespace Research.Knowledge.Api.Endpoints;

/// <summary>Body of <c>POST /knowledge/passages/search</c>.</summary>
public sealed record SearchPassagesRequest(Guid? JobId, IReadOnlyList<string>? Queries, int? TopK);

/// <summary>The matching passages, best first.</summary>
public sealed record SearchPassagesResponse(IReadOnlyList<PassageResponse> Passages);

/// <summary>One passage; <see cref="Score"/> is cosine similarity.</summary>
public sealed record PassageResponse(Guid DocumentId, string Url, string Title, string Text, double Score);

/// <summary>
/// <c>POST /knowledge/passages/search</c>: the <c>topK</c> passages of the job's documents closest to any query. A job
/// without documents returns an empty list.
/// </summary>
public static class SearchPassagesEndpoint
{
    public const string Pattern = "/passages/search";
    public const string Name = "SearchPassages";

    public static async Task<IResult> HandleAsync(
        SearchPassagesRequest? request,
        SearchPassagesHandler handler,
        IOptionsSnapshot<KnowledgeOptions> options,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(context);

        var settings = options.Value;
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (request?.JobId is null || request.JobId == Guid.Empty)
        {
            errors["jobId"] = ["The job id is required."];
        }

        var queries = request?.Queries?.Where(static q => !string.IsNullOrWhiteSpace(q)).ToList() ?? [];
        if (queries.Count == 0 || queries.Count > settings.MaxQueries)
        {
            errors["queries"] = [$"Send 1 to {settings.MaxQueries} non-empty queries."];
        }
        else if (queries.Any(q => q.Length > settings.MaxQueryLength))
        {
            errors["queries"] = [$"Each query is at most {settings.MaxQueryLength} characters."];
        }

        if (request?.TopK is not { } topK || topK < 1 || topK > settings.MaxTopK)
        {
            errors["topK"] = [$"Use 1 to {settings.MaxTopK}."];
        }

        if (errors.Count > 0)
        {
            return ProblemResults.Validation(errors, context);
        }

        var result = await handler.HandleAsync(new SearchPassagesQuery(request!.JobId!.Value, queries, request.TopK!.Value), cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return ProblemResults.From(result.Error, context);
        }

        var passages = result.Value
            .Select(static m => new PassageResponse(m.DocumentId, m.Url, m.Title, m.Text, m.Score))
            .ToList();
        return TypedResults.Ok(new SearchPassagesResponse(passages));
    }
}
