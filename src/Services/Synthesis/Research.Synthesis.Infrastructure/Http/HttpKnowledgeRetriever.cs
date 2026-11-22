using System.Net.Http.Json;
using Research.Synthesis.Application.Common.Interfaces;
using Research.Synthesis.Application;

namespace Research.Synthesis.Infrastructure.Http;

/// <summary>
/// Calls <c>POST /knowledge/passages/search</c>. Non-success statuses throw, so the message is retried; the
/// Knowledge API returns an empty list (not an error) when the job has no passages.
/// </summary>
public sealed class HttpKnowledgeRetriever(HttpClient http) : IKnowledgeRetriever
{
    public const string SearchPath = "/knowledge/passages/search";

    public async Task<IReadOnlyList<RetrievedPassage>> RetrieveAsync(
        Guid jobId,
        IReadOnlyList<string> queries,
        int topK,
        CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            SearchPath,
            new PassageSearchRequest(jobId, queries, topK),
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<PassageSearchResponse>(cancellationToken).ConfigureAwait(false);
        return (body?.Passages ?? [])
            .Select(static p => new RetrievedPassage(p.DocumentId, p.Url, p.Title, p.Text, p.Score))
            .ToList();
    }

    private sealed record PassageSearchRequest(Guid JobId, IReadOnlyList<string> Queries, int TopK);

    private sealed record PassageSearchResponse(IReadOnlyList<PassageDto>? Passages);

    private sealed record PassageDto(Guid DocumentId, string Url, string Title, string Text, double Score);
}
