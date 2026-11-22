using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Research.Application.UnitTests.TestSupport;
using Research.Knowledge.Application;
using Research.Knowledge.Application.Common.Interfaces;
using Research.Knowledge.Application.Observability;
using Research.Knowledge.Domain;
using Research.Knowledge.Domain.Policies;
using Research.Knowledge.Application.IndexDocument;
using Research.Knowledge.Application.SearchPassages;

namespace Research.Application.UnitTests.Knowledge;

public sealed class TextChunkerTests
{
    [Fact]
    public void ShortText_IsOneChunk() =>
        TextChunker.Split("One paragraph.\n\nAnother one.", 1200, 200, 10).ShouldBe(["One paragraph.\n\nAnother one."]);

    [Fact]
    public void Empty_HasNoChunks() => TextChunker.Split("  \n\n  ", 1200, 200, 10).ShouldBeEmpty();

    [Fact]
    public void LongText_RespectsSizeAndOverlaps()
    {
        var paragraphs = Enumerable.Range(0, 40).Select(static i => $"Paragraph {i} " + new string('x', 100) + ".");
        var text = string.Join("\n\n", paragraphs);

        var chunks = TextChunker.Split(text, chunkChars: 500, overlapChars: 100, maxChunks: 100);

        chunks.Count.ShouldBeGreaterThan(1);
        chunks.ShouldAllBe(static c => c.Length <= 500 + 100 + 2);
        for (var i = 1; i < chunks.Count; i++)
        {
            var previousTail = chunks[i - 1][^20..];
            chunks[i].ShouldContain(previousTail);
        }
    }

    [Fact]
    public void LongParagraph_IsCutAtSentences()
    {
        var sentence = "This is a sentence with several words in it. ";
        var paragraph = string.Concat(Enumerable.Repeat(sentence, 60));

        var chunks = TextChunker.Split(paragraph, chunkChars: 400, overlapChars: 0, maxChunks: 100);

        chunks.Count.ShouldBeGreaterThan(1);
        chunks.Take(chunks.Count - 1).ShouldAllBe(static c => c.EndsWith('.'));
    }

    [Fact]
    public void MaxChunks_Caps() =>
        TextChunker.Split(string.Join("\n\n", Enumerable.Repeat(new string('a', 300), 50)), 400, 0, 3).Count.ShouldBe(3);
}

public sealed class SearchPassagesHandlerTests
{
    private readonly FakeRepository _repository = new();

    [Fact]
    public async Task MergesQueries_KeepsBestScore_AndCaps()
    {
        var passageA = Guid.NewGuid();
        var passageB = Guid.NewGuid();
        var passageC = Guid.NewGuid();
        _repository.Results.Enqueue([Match(passageA, 0.4), Match(passageB, 0.9)]);
        _repository.Results.Enqueue([Match(passageA, 0.8), Match(passageC, 0.5)]);

        var result = await CreateHandler().HandleAsync(new SearchPassagesQuery(Guid.NewGuid(), ["q1", "q2"], TopK: 2), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Select(static m => (m.PassageId, m.Score)).ShouldBe([(passageB, 0.9), (passageA, 0.8)]);
    }

    [Fact]
    public async Task DuplicateQueries_AreSearchedOnce()
    {
        await CreateHandler().HandleAsync(new SearchPassagesQuery(Guid.NewGuid(), ["same", " SAME ", ""], TopK: 3), CancellationToken.None);
        _repository.Calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task TopKOutOfRange_IsInvalid(int topK)
    {
        var error = await CreateValidator().ValidateAsync(new SearchPassagesQuery(Guid.NewGuid(), ["q"], topK), CancellationToken.None);
        error!.Code.ShouldBe(KnowledgeErrors.InvalidSearch.Code);
    }

    [Fact]
    public async Task EmptyJobId_IsInvalid()
    {
        var error = await CreateValidator().ValidateAsync(new SearchPassagesQuery(Guid.Empty, ["q"], 3), CancellationToken.None);
        error!.Code.ShouldBe(KnowledgeErrors.InvalidSearch.Code);
    }

    private SearchPassagesHandler CreateHandler() => new(
        _repository,
        new FakeEmbeddingGenerator(),
        new KnowledgeMetrics(TestMeters.Create()),
        new FakeTimeProvider(),
        NullLogger<SearchPassagesHandler>.Instance);

    private static SearchPassagesQueryValidator CreateValidator() => new(TestOptions.Of(new KnowledgeOptions()));

    private static PassageMatch Match(Guid id, double score) => new(id, Guid.NewGuid(), "https://e.com", "t", "text", score);

    private sealed class FakeRepository : IKnowledgeRepository
    {
        public Queue<IReadOnlyList<PassageMatch>> Results { get; } = new();

        public int Calls { get; private set; }

        public Task<IReadOnlyList<PassageMatch>> SearchAsync(Guid jobId, float[] embedding, string embeddingModel, int topK, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : (IReadOnlyList<PassageMatch>)[]);
        }

        public Task<StoredDocument?> FindDocumentAsync(string contentHash, string embeddingModel, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<(StoredDocument Document, bool Inserted)> InsertDocumentAsync(KnowledgeDocument document, IReadOnlyList<Passage> passages, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task LinkAsync(JobDocumentLink link, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}

public sealed class PrepareDocumentHandlerTests
{
    [Fact]
    public async Task KnownContent_IsReusedWithoutReadingTheBlob()
    {
        var existing = new StoredDocument(Guid.NewGuid(), 7);
        var store = new InMemoryDocumentStore();
        var handler = Create(new LookupRepository(existing), store);

        var result = await handler.HandleAsync(new PrepareDocumentCommand("https://e.com", "t", "pages/missing.txt", "hash"), CancellationToken.None);

        result.Value!.Existing.ShouldBe(existing);
        result.Value.NewDocument.ShouldBeNull();
    }

    [Fact]
    public async Task MissingBlob_IsDocumentMissing()
    {
        var handler = Create(new LookupRepository(null), new InMemoryDocumentStore());
        var result = await handler.HandleAsync(new PrepareDocumentCommand("https://e.com", "t", "pages/missing.txt", "hash"), CancellationToken.None);
        result.Error!.Code.ShouldBe(KnowledgeErrors.DocumentMissing.Code);
    }

    [Fact]
    public async Task NewContent_IsChunkedAndEmbedded()
    {
        var store = new InMemoryDocumentStore();
        store.Blobs["pages/h.txt"] = string.Join("\n\n", Enumerable.Repeat("A paragraph of readable text about pgvector.", 80));
        var embeddings = new FakeEmbeddingGenerator();
        var handler = Create(new LookupRepository(null), store, embeddings);

        var result = await handler.HandleAsync(new PrepareDocumentCommand("https://e.com", "t", "pages/h.txt", "h"), CancellationToken.None);

        var prepared = result.Value!;
        prepared.NewDocument!.EmbeddingModel.ShouldBe(embeddings.ModelId);
        prepared.NewDocument.PassageCount.ShouldBe(prepared.Passages.Count);
        prepared.Passages.Count.ShouldBeGreaterThan(1);
        prepared.Passages.Select(static p => p.Ordinal).ShouldBe(Enumerable.Range(0, prepared.Passages.Count));
        prepared.Passages.ShouldAllBe(p => p.Embedding.Length == embeddings.Dimensions);
    }

    private static PrepareDocumentHandler Create(
        IKnowledgeRepository repository,
        InMemoryDocumentStore store,
        FakeEmbeddingGenerator? embeddings = null) => new(
            repository,
            store,
            embeddings ?? new FakeEmbeddingGenerator(),
            TestOptions.Of(new KnowledgeOptions()),
            new FakeTimeProvider(),
            NullLogger<PrepareDocumentHandler>.Instance);

    private sealed class LookupRepository(StoredDocument? existing) : IKnowledgeRepository
    {
        public Task<StoredDocument?> FindDocumentAsync(string contentHash, string embeddingModel, CancellationToken cancellationToken) =>
            Task.FromResult(existing);

        public Task<(StoredDocument Document, bool Inserted)> InsertDocumentAsync(KnowledgeDocument document, IReadOnlyList<Passage> passages, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task LinkAsync(JobDocumentLink link, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PassageMatch>> SearchAsync(Guid jobId, float[] embedding, string embeddingModel, int topK, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
