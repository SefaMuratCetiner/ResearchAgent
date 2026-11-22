namespace Research.Knowledge.Domain;

/// <summary>A job uses a document, found at <see cref="Url"/>.</summary>
public sealed record JobDocumentLink(
    Guid JobId,
    Guid DocumentId,
    string Url,
    string Title,
    DateTimeOffset CreatedAt = default,
    string CreatedBy = "");
