using Research.Jobs.Application.Common.Services;
using Research.Shared.Bus.Messaging;
using Research.Shared.Contracts.Knowledge;

namespace Research.Jobs.Application.AdvanceOnDocumentIndexed;

/// <summary>A page was indexed: its fetch step succeeded and gathering may now end.</summary>
public sealed class DocumentIndexedMessageHandler(StepReplyProcessor replies, JobProgressService progress) : IMessageHandler<DocumentIndexed>
{
    public Task HandleAsync(DocumentIndexed message, MessageContext context, CancellationToken cancellationToken) =>
        replies.ProcessAsync(message, context, job => progress.ApplyDocumentIndexedAsync(job, cancellationToken), cancellationToken);
}
