namespace Research.Knowledge.Api.SearchPassages;

/// <summary>One passage; <see cref="Score"/> is cosine similarity.</summary>
public sealed record PassageResponse(Guid DocumentId, string Url, string Title, string Text, double Score);
