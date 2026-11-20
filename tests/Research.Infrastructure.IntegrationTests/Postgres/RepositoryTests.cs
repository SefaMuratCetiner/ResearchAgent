using Research.Fetching.Domain;
using Research.Fetching.Infrastructure.Persistence;
using Research.Knowledge.Domain;
using Research.Knowledge.Infrastructure.Persistence;
using Research.Providers.MockEmbeddings;
using Research.Providers.Postgres.Transactions;

namespace Research.Infrastructure.IntegrationTests.Postgres;

public sealed class FetchedDocumentRepositoryTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Upsert_SameJobAndUrl_KeepsTheFirstId()
    {
        var transactions = new PostgresTransactionFactory(postgres.Fetching);
        var repository = new FetchedDocumentRepository();
        var jobId = Guid.NewGuid();

        var first = await UpsertAsync(transactions, repository, Document(jobId, "hash-1"));
        var second = await UpsertAsync(transactions, repository, Document(jobId, "hash-2"));
        var otherJob = await UpsertAsync(transactions, repository, Document(Guid.NewGuid(), "hash-1"));

        second.ShouldBe(first);
        otherJob.ShouldNotBe(first);
    }

    private static async Task<Guid> UpsertAsync(PostgresTransactionFactory transactions, FetchedDocumentRepository repository, FetchedDocument document)
    {
        await using var transaction = await transactions.BeginAsync(CancellationToken.None);
        var id = await repository.UpsertAsync(document, transaction, CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);
        return id;
    }

    private static FetchedDocument Document(Guid jobId, string contentHash) => new()
    {
        Id = Guid.CreateVersion7(),
        JobId = jobId,
        Url = "https://example.com/page",
        UrlHash = new string('a', 64),
        FinalUrl = "https://example.com/page",
        ContentHash = contentHash.PadRight(64, '0'),
        Title = "Page",
        MimeType = "text/html",
        SizeBytes = 1000,
        CharacterCount = 900,
        BlobKey = $"pages/{contentHash}.txt",
        CreatedAt = DateTimeOffset.UtcNow,
        CreatedBy = "test",
    };
}

public sealed class KnowledgeRepositoryTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Search_IsScopedToTheJob_AndRankedBySimilarity()
    {
        var repository = new KnowledgeRepository(postgres.Knowledge);
        var transactions = new PostgresTransactionFactory(postgres.Knowledge);
        var jobA = Guid.NewGuid();
        var jobB = Guid.NewGuid();

        var document = await StoreAsync(
            repository,
            transactions,
            jobA,
            ["The HNSW index in pgvector builds a layered proximity graph.", "Sourdough bread needs a long fermentation."]);

        var query = HashingEmbeddingGenerator.Embed("how does the pgvector hnsw index work");
        var forA = await repository.SearchAsync(jobA, query, HashingEmbeddingGenerator.Model, 5, CancellationToken.None);
        var forB = await repository.SearchAsync(jobB, query, HashingEmbeddingGenerator.Model, 5, CancellationToken.None);

        forA.Count.ShouldBe(2);
        forA[0].Text.ShouldContain("HNSW");
        forA[0].Score.ShouldBeGreaterThan(forA[1].Score);
        forA.ShouldAllBe(m => m.DocumentId == document.Id && m.Url == "https://example.com/doc");
        forB.ShouldBeEmpty();
    }

    [Fact]
    public async Task SameContent_IsStoredOnce_AndCanBeLinkedToAnotherJob()
    {
        var repository = new KnowledgeRepository(postgres.Knowledge);
        var transactions = new PostgresTransactionFactory(postgres.Knowledge);
        var hash = Guid.NewGuid().ToString("N").PadRight(64, '0');

        var first = await StoreAsync(repository, transactions, Guid.NewGuid(), ["text one"], hash);
        (await repository.FindDocumentAsync(hash, HashingEmbeddingGenerator.Model, CancellationToken.None)).ShouldBe(first);

        await using var transaction = await transactions.BeginAsync(CancellationToken.None);
        var (again, inserted) = await repository.InsertDocumentAsync(Document(hash, 1), [NewPassage(0, "text one")], transaction, CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);

        inserted.ShouldBeFalse();
        again.ShouldBe(first);
    }

    private static async Task<StoredDocument> StoreAsync(
        KnowledgeRepository repository,
        PostgresTransactionFactory transactions,
        Guid jobId,
        IReadOnlyList<string> texts,
        string? contentHash = null)
    {
        var hash = contentHash ?? Guid.NewGuid().ToString("N").PadRight(64, '0');
        await using var transaction = await transactions.BeginAsync(CancellationToken.None);
        var (stored, inserted) = await repository.InsertDocumentAsync(
            Document(hash, texts.Count),
            texts.Select((t, i) => NewPassage(i, t)).ToList(),
            transaction,
            CancellationToken.None);
        inserted.ShouldBeTrue();
        await repository.LinkAsync(
            new JobDocumentLink(jobId, stored.Id, "https://example.com/doc", "Doc", DateTimeOffset.UtcNow, "test"),
            transaction,
            CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);
        stored.PassageCount.ShouldBe(texts.Count);
        return stored;
    }

    private static KnowledgeDocument Document(string hash, int passages) => new()
    {
        Id = Guid.CreateVersion7(),
        ContentHash = hash,
        EmbeddingModel = HashingEmbeddingGenerator.Model,
        Url = "https://example.com/doc",
        Title = "Doc",
        CharacterCount = 100,
        PassageCount = passages,
        CreatedAt = DateTimeOffset.UtcNow,
        CreatedBy = "test",
    };

    private static Passage NewPassage(int ordinal, string text) =>
        new(Guid.CreateVersion7(), ordinal, text, HashingEmbeddingGenerator.Embed(text));
}
