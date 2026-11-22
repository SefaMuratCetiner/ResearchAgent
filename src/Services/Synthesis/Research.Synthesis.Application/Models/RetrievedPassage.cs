namespace Research.Synthesis.Application;

/// <summary>A passage retrieved from Knowledge to ground report synthesis and citation numbering.</summary>
public sealed record RetrievedPassage(Guid DocumentId, string Url, string Title, string Text, double Score);
