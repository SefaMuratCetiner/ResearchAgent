using Microsoft.Extensions.Logging;
using Research.Shared.Contracts.Fetching;
using Research.Shared.Contracts.Knowledge;
using Research.Shared.Contracts.Steps;
using Research.Knowledge.Application.Common.Interfaces;
using Research.Knowledge.Application.Observability;
using Research.Knowledge.Domain;
using Research.Shared.Bus.Mediation;
using Research.Shared.Bus.Messaging;
using Research.Shared.Bus.Transactions;

namespace Research.Knowledge.Application.IndexDocument;

/// <summary>
/// Answers <c>DocumentFetched</c> with <c>DocumentIndexed</c> or <c>StepFailed</c>. Fast-path inbox deduplication runs
/// in <c>InboxBehavior</c> before any work. Preparation (blob read, chunking, embedding) runs outside the transaction;
/// one short transaction then atomically records the inbox entry, the document (unless the same content is already
/// indexed), the job link and the reply in the outbox.
/// </summary>
public sealed partial class DocumentFetchedMessageHandler(
    ISender sender,
    IKnowledgeRepository repository,
    IUnitOfWork unitOfWork,
    IInbox inbox,
    IOutbox outbox,
    IEnvelopeFactory envelopes,
    KnowledgeMetrics metrics,
    ILogger<DocumentFetchedMessageHandler> logger) : IMessageHandler<DocumentFetched>
{
    public const string ServiceName = "knowledge";

    public async Task HandleAsync(DocumentFetched message, MessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        var prepared = await sender.SendAsync(
            new PrepareDocumentCommand(message.Url, message.Title, message.BlobKey, message.ContentHash),
            cancellationToken).ConfigureAwait(false);

        await using var _ = await unitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);
        if (!await inbox.TryRecordAsync(context.MessageId, context.Consumer, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        OutgoingEnvelope reply;
        string outcome;
        if (!prepared.IsSuccess)
        {
            outcome = prepared.Error.Code;
            reply = envelopes.Create(new StepFailed(message.JobId, message.StepKey, prepared.Error.Code, ServiceName));
            LogFailed(logger, message.JobId, message.Url, prepared.Error.Code);
        }
        else
        {
            var (documentId, passageCount, reused) = await StoreAsync(prepared.Value, cancellationToken).ConfigureAwait(false);
            await repository.LinkAsync(
                new JobDocumentLink(message.JobId, documentId, message.Url, message.Title),
                cancellationToken).ConfigureAwait(false);

            outcome = reused ? "reused" : "indexed";
            reply = envelopes.Create(new DocumentIndexed(message.JobId, message.StepKey, documentId, passageCount, reused));
            LogIndexed(logger, message.JobId, message.Url, documentId, passageCount, reused);
        }

        await outbox.EnqueueAsync(reply, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        metrics.DocumentProcessed(outcome);
    }

    private async Task<(Guid DocumentId, int PassageCount, bool Reused)> StoreAsync(
        PreparedDocument prepared,
        CancellationToken cancellationToken)
    {
        if (prepared.Existing is { } existing)
        {
            return (existing.Id, existing.PassageCount, true);
        }

        var (stored, inserted) = await repository.InsertDocumentAsync(prepared.NewDocument!, prepared.Passages, cancellationToken)
            .ConfigureAwait(false);
        if (inserted)
        {
            metrics.PassagesStored(stored.PassageCount);
        }

        // Not inserted: another replica indexed the same content between the lookup and this insert.
        return (stored.Id, stored.PassageCount, !inserted);
    }

    [LoggerMessage(EventId = 6010, Level = LogLevel.Information, Message = "Indexed {Url} for job {JobId} as document {DocumentId}: {PassageCount} passages, reused {Reused}")]
    private static partial void LogIndexed(ILogger logger, Guid jobId, string url, Guid documentId, int passageCount, bool reused);

    [LoggerMessage(EventId = 6011, Level = LogLevel.Information, Message = "Indexing {Url} for job {JobId} failed permanently: {ErrorCode}")]
    private static partial void LogFailed(ILogger logger, Guid jobId, string url, string errorCode);
}
