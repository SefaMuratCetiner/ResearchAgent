using Research.Jobs.Application.Common.Services;
using Research.Shared.Bus.Messaging;
using Research.Shared.Contracts.Search;

namespace Research.Jobs.Application.AdvanceOnResultsFound;

/// <summary>A search answered: fetch steps start for its new URLs.</summary>
public sealed class ResultsFoundMessageHandler(StepReplyProcessor replies, JobProgressService progress) : IMessageHandler<ResultsFound>
{
    public Task HandleAsync(ResultsFound message, MessageContext context, CancellationToken cancellationToken) =>
        replies.ProcessAsync(message, context, job => progress.ApplyResultsAsync(job, message, cancellationToken), cancellationToken);
}
