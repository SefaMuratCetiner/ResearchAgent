using System.Text;
using Research.Shared.Ports.Embeddings;

namespace Research.Providers.MockEmbeddings;

/// <summary>
/// Feature-hashing embeddings: every lower-cased word and word bigram is hashed (FNV-1a) into one of
/// <see cref="Dimensions"/> buckets with a hash-derived sign, and the vector is L2-normalized, so cosine similarity
/// tracks shared vocabulary. Deterministic and free; the model id keeps these vectors apart from a real model's.
/// </summary>
public sealed class HashingEmbeddingGenerator : IEmbeddingGenerator
{
    public const string Model = "mock-hashing-v1";
    public const int VectorDimensions = 384;

    public string ModelId => Model;

    public int Dimensions => VectorDimensions;

    public Task<IReadOnlyList<float[]>> GenerateAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texts);
        var vectors = new float[texts.Count][];
        for (var i = 0; i < texts.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            vectors[i] = Embed(texts[i]);
        }

        return Task.FromResult<IReadOnlyList<float[]>>(vectors);
    }

    public static float[] Embed(string text)
    {
        var vector = new float[VectorDimensions];
        string? previous = null;
        foreach (var word in Tokenize(text))
        {
            Add(vector, word, 1f);
            if (previous is not null)
            {
                Add(vector, previous + " " + word, 0.5f);
            }

            previous = word;
        }

        Normalize(vector);
        return vector;
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        var current = new StringBuilder();
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                current.Append(char.ToLowerInvariant(c));
            }
            else if (current.Length > 0)
            {
                if (current.Length > 1)
                {
                    yield return current.ToString();
                }

                current.Clear();
            }
        }

        if (current.Length > 1)
        {
            yield return current.ToString();
        }
    }

    private static void Add(float[] vector, string feature, float weight)
    {
        var hash = Fnv1a(feature);
        var bucket = (int)(hash % (uint)vector.Length);
        var sign = (hash & 0x8000_0000u) == 0 ? 1f : -1f;
        vector[bucket] += sign * weight;
    }

    private static uint Fnv1a(string value)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            hash ^= b;
            hash *= prime;
        }

        return hash;
    }

    private static void Normalize(float[] vector)
    {
        double sum = 0;
        foreach (var v in vector)
        {
            sum += v * v;
        }

        if (sum <= 0)
        {
            return;
        }

        var length = (float)Math.Sqrt(sum);
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= length;
        }
    }
}
