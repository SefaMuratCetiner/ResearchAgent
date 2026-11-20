using Dapper;
using Research.Fetching.Application.Common.Interfaces;
using Research.Fetching.Domain;
using Research.Providers.Postgres.Transactions;
using Research.Shared.Core.Transactions;

namespace Research.Fetching.Infrastructure.Persistence;

/// <summary>Dapper repository over <c>fetched_documents</c>.</summary>
public sealed class FetchedDocumentRepository : IFetchedDocumentRepository
{
    private const string UpsertSql = """
        INSERT INTO fetched_documents
            (id, job_id, url, url_hash, final_url, content_hash, title, mime_type, size_bytes, character_count, blob_key, created_at, created_by)
        VALUES
            (@Id, @JobId, @Url, @UrlHash, @FinalUrl, @ContentHash, @Title, @MimeType, @SizeBytes, @CharacterCount, @BlobKey, @CreatedAt, @CreatedBy)
        ON CONFLICT (job_id, url_hash) DO UPDATE SET
            final_url = EXCLUDED.final_url,
            content_hash = EXCLUDED.content_hash,
            title = EXCLUDED.title,
            mime_type = EXCLUDED.mime_type,
            size_bytes = EXCLUDED.size_bytes,
            character_count = EXCLUDED.character_count,
            blob_key = EXCLUDED.blob_key,
            updated_at = EXCLUDED.created_at,
            updated_by = EXCLUDED.created_by
        RETURNING id
        """;

    public async Task<Guid> UpsertAsync(FetchedDocument document, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        var scope = PostgresTransactionScope.From(transaction);
        return await scope.Connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            UpsertSql,
            new
            {
                document.Id,
                document.JobId,
                document.Url,
                document.UrlHash,
                document.FinalUrl,
                document.ContentHash,
                document.Title,
                document.MimeType,
                document.SizeBytes,
                document.CharacterCount,
                document.BlobKey,
                document.CreatedAt,
                document.CreatedBy,
            },
            scope.Transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}
