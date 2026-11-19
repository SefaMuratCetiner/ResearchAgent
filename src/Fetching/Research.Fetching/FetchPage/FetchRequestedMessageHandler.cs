using Research.Contracts.Fetching;
using Research.Contracts.Steps;
using Research.Fetching.Domain;
using Research.Messaging;
using Research.SharedKernel.Actors;
using Research.SharedKernel.Messaging;
using Research.SharedKernel.Transactions;

namespace Research.Fetching.FetchPage;

/// <summary>
/// Answers <c>FetchRequested</c>. A redelivered message that was already recorded is skipped before any download.
/// Otherwise the page is fetched (blob written first), then one transaction records the inbox entry, the
/// <c>fetched_documents</c> row and the reply (<c>DocumentFetched</c> or <c>StepFailed</c>) in the outbox.
/// Transient failures throw and the message is retried.
/// </summary>
public sealed class FetchRequestedMessageHandler(
    FetchPageHandler handler,
    IFetchedDocumentRepository documents,
    ITransactionFactory transactions,
    IInbox inbox,
    IOutbox outbox,
    EnvelopeFactory envelopes,
    TimeProvider timeProvider,
    ICurrentActor actor) : IMessageHandler<FetchRequested>
{
    public const string ServiceName = "fetching";

    public async Task HandleAsync(FetchRequested message, MessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        if (await inbox.HasProcessedAsync(context.MessageId, context.Consumer, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var result = await handler.HandleAsync(new FetchPageCommand(message.JobId, message.Url, message.Title), cancellationToken)
            .ConfigureAwait(false);

        await using var transaction = await transactions.BeginAsync(cancellationToken).ConfigureAwait(false);
        if (!await inbox.TryRecordAsync(context.MessageId, context.Consumer, transaction, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        OutgoingEnvelope reply;
        if (result.IsSuccess)
        {
            var page = result.Value;
            var now = timeProvider.GetUtcNow();
            var documentId = await documents.UpsertAsync(
                new FetchedDocument
                {
                    Id = Guid.CreateVersion7(now),
                    JobId = message.JobId,
                    Url = page.Url,
                    UrlHash = page.UrlHash,
                    FinalUrl = page.FinalUrl,
                    ContentHash = page.ContentHash,
                    Title = page.Title,
                    MimeType = page.MimeType,
                    SizeBytes = page.SizeBytes,
                    CharacterCount = page.CharacterCount,
                    BlobKey = page.BlobKey,
                    CreatedAt = now,
                    CreatedBy = actor.Id,
                },
                transaction,
                cancellationToken).ConfigureAwait(false);

            reply = envelopes.Create(new DocumentFetched(
                message.JobId,
                message.StepKey,
                documentId,
                page.Url,
                page.Title,
                page.BlobKey,
                page.ContentHash,
                page.CharacterCount));
        }
        else
        {
            reply = envelopes.Create(new StepFailed(message.JobId, message.StepKey, result.Error.Code, ServiceName));
        }

        await outbox.EnqueueAsync(reply, transaction, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
