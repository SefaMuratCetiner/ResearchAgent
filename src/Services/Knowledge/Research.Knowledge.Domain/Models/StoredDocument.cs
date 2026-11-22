namespace Research.Knowledge.Domain;

/// <summary>An indexed document found by content hash.</summary>
public sealed record StoredDocument(Guid Id, int PassageCount);
