using Research.Knowledge.Infrastructure.Persistence;
using Research.Providers.MockEmbeddings;

namespace Research.Infrastructure.UnitTests.Knowledge;

public sealed class HashingEmbeddingGeneratorTests
{
    [Fact]
    public void Vectors_AreNormalizedAndDeterministic()
    {
        var a = HashingEmbeddingGenerator.Embed("pgvector hnsw index");
        var b = HashingEmbeddingGenerator.Embed("pgvector hnsw index");

        a.Length.ShouldBe(HashingEmbeddingGenerator.VectorDimensions);
        a.ShouldBe(b);
        Math.Sqrt(a.Sum(static v => (double)v * v)).ShouldBe(1.0, 1e-5);
    }

    [Fact]
    public void SimilarTexts_AreCloserThanUnrelatedTexts()
    {
        var query = HashingEmbeddingGenerator.Embed("how does the hnsw index in pgvector work");
        var related = HashingEmbeddingGenerator.Embed("the pgvector hnsw index builds a layered graph");
        var unrelated = HashingEmbeddingGenerator.Embed("baking sourdough bread at home");

        Cosine(query, related).ShouldBeGreaterThan(Cosine(query, unrelated));
    }

    private static double Cosine(float[] a, float[] b) => a.Zip(b, static (x, y) => (double)x * y).Sum();
}

public sealed class VectorLiteralTests
{
    [Fact]
    public void ToVectorLiteral_IsInvariantPgvectorText() =>
        KnowledgeRepository.ToVectorLiteral([0.5f, -1.25f, 0f]).ShouldBe("[0.5,-1.25,0]");
}
