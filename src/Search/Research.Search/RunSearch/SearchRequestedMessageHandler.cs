using Research.Contracts.Search;
using Research.Contracts.Steps;
using Research.Messaging.Sending;
using Research.SharedKernel.Messaging;

namespace Research.Search.RunSearch;

/// <summary>Answers <c>SearchRequested</c> with <c>ResultsFound</c> (possibly empty), or <c>StepFailed</c>.</summary>
public sealed class SearchRequestedMessageHandler(RunSearchHandler handler, IMessageSender sender) : IMessageHandler<SearchRequested>
{
    public const string ServiceName = "search";

    public async Task HandleAsync(SearchRequested message, MessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var result = await handler.HandleAsync(new RunSearchCommand(message.Query, message.MaxResults), cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            await sender.SendAsync(new StepFailed(message.JobId, message.StepKey, result.Error.Code, ServiceName), cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var hits = result.Value.Hits.Select(static h => new SearchResultItem(h.Url, h.Title, h.PageAge)).ToList();
        await sender.SendAsync(
            new ResultsFound(message.JobId, message.StepKey, hits, result.Value.InputTokens, result.Value.OutputTokens),
            cancellationToken).ConfigureAwait(false);
    }
}
