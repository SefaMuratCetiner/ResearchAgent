using Research.Shared.Contracts.Search;
using Research.Jobs.Application.Common.Services;
using Research.Jobs.Domain;
using Research.Shared.Bus.Messaging;

namespace Research.Jobs.Application.AdvanceOnResultsFound;

/// <summary>
/// A search answered: record its tokens, start a fetch step per new URL, then check whether gathering ended.
/// </summary>
public sealed class ResultsFoundMessageHandler(
    StepReplyProcessor replyProcessor,
    FetchStepService fetchSteps,
    GatheringPhaseCompleter gatheringCompleter) : IMessageHandler<ResultsFound>
{
    public Task HandleAsync(ResultsFound message, MessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return replyProcessor.ProcessAsync(
            message.JobId,
            message.StepKey,
            StepStatus.Succeeded,
            stepErrorCode: null,
            context,
            async job =>
            {
                job.RecordTokens(message.InputTokens + message.OutputTokens);
                if (job.Status != JobStatus.Gathering)
                {
                    return;
                }

                await fetchSteps.StartFetchesAsync(job, message.Hits, cancellationToken).ConfigureAwait(false);
                await gatheringCompleter.TryCompleteAsync(job, forcedReason: null, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }
}
