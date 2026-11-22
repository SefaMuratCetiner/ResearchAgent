namespace Research.Knowledge.Api.SearchPassages;

/// <summary>Body of <c>POST /knowledge/passages/search</c>.</summary>
public sealed record SearchPassagesRequest(Guid? JobId, IReadOnlyList<string>? Queries, int? TopK);
