namespace Research.SharedKernel.Embeddings;

/// <summary>
/// Turns text into vectors. Stored vectors carry <see cref="ModelId"/>, and searches only compare vectors of the same
/// model, so swapping the generator never mixes incompatible embeddings.
/// </summary>
public interface IEmbeddingGenerator
{
    string ModelId { get; }

    int Dimensions { get; }

    /// <returns>One vector per input text, in the same order, each of <see cref="Dimensions"/> length.</returns>
    Task<IReadOnlyList<float[]>> GenerateAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken);
}
