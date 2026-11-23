using Research.Shared.Contracts.Fetching;
using Research.Shared.Contracts.Steps;
using Research.Fetching.Application.Common.Interfaces;
using Research.Fetching.Domain;
using Research.Shared.Bus.Messaging;
using Research.Shared.Bus.Transactions;

namespace Research.Fetching.Application.FetchPage;

/// <summary>
/// Answers <c>FetchRequested</c>. Fast-path inbox deduplication runs in <c>InboxBehavior</c> before any download;
/// after fetching (blob written first), one short transaction records the inbox entry atomically (guarding concurrent
/// delivery races), the <c>fetched_documents</c> row, and the reply (<c>DocumentFetched</c> or <c>StepFailed</c>) in
/// the outbox. Transient failures throw and the message is retried.
/// </summary>
public sealed class FetchRequestedMessageHandler(
    PageFetchService fetchService,
    IFetchedDocumentRepository documents,
    IUnitOfWork unitOfWork,
    IInbox inbox,
    IOutbox outbox,
    IEnvelopeFactory envelopes,
    TimeProvider timeProvider) : IMessageHandler<FetchRequested>
{
    public const string ServiceName = "fetching";

    public async Task HandleAsync(FetchRequested message, MessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        var result = await fetchService.FetchAsync(message, cancellationToken).ConfigureAwait(false);

        await using var _ = await unitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);
        if (!await inbox.TryRecordAsync(context.MessageId, context.Consumer, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        OutgoingEnvelope reply;
        if (result.IsSuccess)
        {
            var page = result.Value;
            var documentId = await documents.UpsertAsync(
                new FetchedDocument
                {
                    Id = Guid.CreateVersion7(timeProvider.GetUtcNow()),
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
                },
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

        await outbox.EnqueueAsync(reply, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
