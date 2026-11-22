namespace Research.Knowledge.Domain;

/// <summary>A passage found by a similarity search; <see cref="Score"/> is cosine similarity (1 = identical).</summary>
public sealed record PassageMatch(Guid PassageId, Guid DocumentId, string Url, string Title, string Text, double Score);
