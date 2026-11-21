using System.Globalization;
using System.Text;
using Dapper;
using Research.Knowledge.Application.Common.Interfaces;
using Research.Knowledge.Domain;
using Research.Providers.Postgres.Transactions;

namespace Research.Knowledge.Infrastructure.Persistence;

/// <summary>
/// Dapper repository over <c>documents</c>, <c>passages</c> and <c>job_documents</c>. Embeddings are sent as pgvector
/// text literals and cast in SQL; passages are inserted in one statement with <c>unnest</c>.
/// </summary>
public sealed class KnowledgeRepository(PostgresUnitOfWork unitOfWork) : IKnowledgeRepository
{
    private const string FindSql = """
        SELECT id, passage_count FROM documents WHERE content_hash = @ContentHash AND embedding_model = @EmbeddingModel
        """;

    private const string InsertDocumentSql = """
        INSERT INTO documents (id, content_hash, embedding_model, url, title, character_count, passage_count, created_at, created_by)
        VALUES (@Id, @ContentHash, @EmbeddingModel, @Url, @Title, @CharacterCount, @PassageCount, @CreatedAt, @CreatedBy)
        ON CONFLICT (content_hash, embedding_model) DO NOTHING
        """;

    private const string InsertPassagesSql = """
        INSERT INTO passages (id, document_id, ordinal, text, embedding)
        SELECT p.id, @DocumentId, p.ordinal, p.text, p.embedding::vector
        FROM unnest(@Ids::uuid[], @Ordinals::integer[], @Texts::text[], @Embeddings::text[]) AS p(id, ordinal, text, embedding)
        """;

    private const string LinkSql = """
        INSERT INTO job_documents (job_id, document_id, url, title, created_at, created_by)
        VALUES (@JobId, @DocumentId, @Url, @Title, @CreatedAt, @CreatedBy)
        ON CONFLICT (job_id, document_id) DO NOTHING
        """;

    private const string SearchSql = """
        SELECT p.id AS passage_id, p.document_id, jd.url, jd.title, p.text, 1 - (p.embedding <=> @Embedding::vector) AS score
        FROM passages p
        JOIN job_documents jd ON jd.document_id = p.document_id AND jd.job_id = @JobId
        JOIN documents d ON d.id = p.document_id AND d.embedding_model = @EmbeddingModel
        ORDER BY p.embedding <=> @Embedding::vector
        LIMIT @TopK
        """;

    public async Task<StoredDocument?> FindDocumentAsync(string contentHash, string embeddingModel, CancellationToken cancellationToken)
    {
        await using var connection = await unitOfWork.DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<DocumentRow>(new CommandDefinition(
            FindSql,
            new { ContentHash = contentHash, EmbeddingModel = embeddingModel },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : new StoredDocument(row.Id, row.PassageCount);
    }

    public async Task<(StoredDocument Document, bool Inserted)> InsertDocumentAsync(
        KnowledgeDocument document,
        IReadOnlyList<Passage> passages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(passages);

        var createdAt = document.CreatedAt == default ? unitOfWork.Now : document.CreatedAt;
        var createdBy = string.IsNullOrWhiteSpace(document.CreatedBy) ? unitOfWork.ActorId : document.CreatedBy;

        var inserted = await unitOfWork.ActiveConnection.ExecuteAsync(new CommandDefinition(
            InsertDocumentSql,
            new
            {
                document.Id,
                document.ContentHash,
                document.EmbeddingModel,
                document.Url,
                document.Title,
                document.CharacterCount,
                document.PassageCount,
                CreatedAt = createdAt,
                CreatedBy = createdBy,
            },
            unitOfWork.ActiveTransaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (inserted == 0)
        {
            // The conflicting insert committed first (ON CONFLICT waits for it), so under READ COMMITTED the next
            // statement sees the row.
            var existing = await unitOfWork.ActiveConnection.QuerySingleAsync<DocumentRow>(new CommandDefinition(
                FindSql,
                new { document.ContentHash, document.EmbeddingModel },
                unitOfWork.ActiveTransaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
            return (new StoredDocument(existing.Id, existing.PassageCount), false);
        }

        await unitOfWork.ActiveConnection.ExecuteAsync(new CommandDefinition(
            InsertPassagesSql,
            new
            {
                DocumentId = document.Id,
                Ids = passages.Select(static p => p.Id).ToArray(),
                Ordinals = passages.Select(static p => p.Ordinal).ToArray(),
                Texts = passages.Select(static p => p.Text).ToArray(),
                Embeddings = passages.Select(static p => ToVectorLiteral(p.Embedding)).ToArray(),
            },
            unitOfWork.ActiveTransaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return (new StoredDocument(document.Id, passages.Count), true);
    }

    public async Task LinkAsync(JobDocumentLink link, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);
        var createdAt = link.CreatedAt == default ? unitOfWork.Now : link.CreatedAt;
        var createdBy = string.IsNullOrWhiteSpace(link.CreatedBy) ? unitOfWork.ActorId : link.CreatedBy;

        await unitOfWork.ActiveConnection.ExecuteAsync(new CommandDefinition(
            LinkSql,
            new
            {
                link.JobId,
                link.DocumentId,
                link.Url,
                link.Title,
                CreatedAt = createdAt,
                CreatedBy = createdBy,
            },
            unitOfWork.ActiveTransaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PassageMatch>> SearchAsync(
        Guid jobId,
        float[] embedding,
        string embeddingModel,
        int topK,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(embedding);
        await using var connection = await unitOfWork.DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<MatchRow>(new CommandDefinition(
            SearchSql,
            new { JobId = jobId, Embedding = ToVectorLiteral(embedding), EmbeddingModel = embeddingModel, TopK = topK },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(static r => new PassageMatch(r.PassageId, r.DocumentId, r.Url, r.Title, r.Text, r.Score)).ToList();
    }

    /// <summary>pgvector's text form: <c>[0.1,-0.2,...]</c>, invariant culture, round-trippable.</summary>
    internal static string ToVectorLiteral(float[] vector)
    {
        var builder = new StringBuilder(vector.Length * 10).Append('[');
        for (var i = 0; i < vector.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(vector[i].ToString("R", CultureInfo.InvariantCulture));
        }

        return builder.Append(']').ToString();
    }

    private sealed class DocumentRow
    {
        public Guid Id { get; set; }

        public int PassageCount { get; set; }
    }

    private sealed class MatchRow
    {
        public Guid PassageId { get; set; }

        public Guid DocumentId { get; set; }

        public string Url { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public double Score { get; set; }
    }
}
