using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Knowledge.Application.Common.Interfaces;
using Research.Knowledge.Domain;
using Research.Knowledge.Domain.Policies;
using Research.Shared.Core.Documents;
using Research.Shared.Core.Embeddings;
using Research.Shared.Bus.Mediation;
using Research.Shared.Kernel.Results;

namespace Research.Knowledge.Application.IndexDocument;

/// <summary>
/// The slow, transaction-free part of indexing: looks up the content hash, and for new content reads the blob, chunks
/// it and embeds the passages in batches. The message handler writes the result in one short transaction.
/// </summary>
public sealed partial class PrepareDocumentHandler(
    IKnowledgeRepository repository,
    IDocumentStore documents,
    IEmbeddingGenerator embeddings,
    IOptionsSnapshot<KnowledgeOptions> options,
    TimeProvider timeProvider,
    ILogger<PrepareDocumentHandler> logger) : ICommandHandler<PrepareDocumentCommand, Result<PreparedDocument>>
{
    public async Task<Result<PreparedDocument>> HandleAsync(PrepareDocumentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var existing = await repository.FindDocumentAsync(command.ContentHash, embeddings.ModelId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Result<PreparedDocument>.Success(PreparedDocument.From(existing));
        }

        var text = await documents.ReadTextAsync(command.BlobKey, cancellationToken).ConfigureAwait(false);
        if (text is null)
        {
            return Result<PreparedDocument>.Failure(KnowledgeErrors.DocumentMissing);
        }

        var settings = options.Value;
        var chunks = TextChunker.Split(text, settings.ChunkChars, settings.ChunkOverlapChars, settings.MaxPassagesPerDocument);
        if (chunks.Count == 0)
        {
            return Result<PreparedDocument>.Failure(KnowledgeErrors.NoPassages);
        }

        var vectors = new List<float[]>(chunks.Count);
        foreach (var batch in chunks.Chunk(settings.EmbeddingBatchSize))
        {
            vectors.AddRange(await embeddings.GenerateAsync(batch, cancellationToken).ConfigureAwait(false));
        }

        if (vectors.Count != chunks.Count || vectors.Any(v => v.Length != embeddings.Dimensions))
        {
            return Result<PreparedDocument>.Failure(KnowledgeErrors.EmbeddingDimensionMismatch);
        }

        var now = timeProvider.GetUtcNow();
        var passages = chunks.Select((chunk, i) => new Passage(Guid.CreateVersion7(now), i, chunk, vectors[i])).ToList();
        var document = new KnowledgeDocument
        {
            Id = Guid.CreateVersion7(now),
            ContentHash = command.ContentHash,
            EmbeddingModel = embeddings.ModelId,
            Url = command.Url,
            Title = command.Title,
            CharacterCount = text.Length,
            PassageCount = passages.Count,
        };

        LogPrepared(logger, command.Url, passages.Count, embeddings.ModelId);
        return Result<PreparedDocument>.Success(new PreparedDocument(null, document, passages));
    }

    [LoggerMessage(EventId = 6000, Level = LogLevel.Information, Message = "Prepared {PassageCount} passages of {Url} with {EmbeddingModel}")]
    private static partial void LogPrepared(ILogger logger, string url, int passageCount, string embeddingModel);
}
