namespace Research.Knowledge.Domain;

/// <summary>A chunk of a document and its embedding.</summary>
public sealed record Passage(Guid Id, int Ordinal, string Text, float[] Embedding);
