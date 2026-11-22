using Microsoft.Extensions.Options;
using Research.Shared.Contracts.Fetching;
using Research.Shared.Contracts.Search;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Application.Common.Services;
using Research.Jobs.Domain;
using Research.Shared.Messaging;
using Research.Shared.Kernel.Security;

namespace Research.Jobs.Application.AdvanceOnResultsFound;

/// <summary>
/// A search answered: record its tokens, start a fetch step per new URL (deduplicated per job by the step key,
/// capped at <see cref="JobsOptions.MaxSources"/>, none once the budget is spent), then check whether gathering ended.
/// </summary>
public sealed class ResultsFoundMessageHandler(
    StepReplyProcessor replyProcessor,
    GatheringPhaseCompleter gatheringCompleter,
    StepDispatcher stepDispatcher,
    IResearchJobRepository jobs,
    IJobStepRepository steps,
    IOptionsSnapshot<JobsOptions> options) : IMessageHandler<ResultsFound>
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
                if (job.RecordTokens(message.InputTokens + message.OutputTokens)
                    && !await jobs.UpdateAsync(job, cancellationToken).ConfigureAwait(false))
                {
                    return;
                }

                if (job.Status != JobStatus.Gathering)
                {
                    return;
                }

                if (!job.BudgetExhausted)
                {
                    var counts = await steps.CountAsync(job.Id, cancellationToken).ConfigureAwait(false);
                    var room = options.Value.MaxSources - counts.FetchTotal;
                    foreach (var hit in message.Hits ?? [])
                    {
                        if (room <= 0)
                        {
                            break;
                        }

                        // Only absolute http(s) URLs of sane length become fetch steps (OWASP ASI02/ASI07).
                        if (!UntrustedText.IsWebUrl(hit.Url))
                        {
                            continue;
                        }

                        var started = await stepDispatcher.TryDispatchAsync(
                            new FetchRequested(job.Id, JobStepKeys.Fetch(hit.Url), hit.Url, UntrustedText.SingleLine(hit.Title)),
                            cancellationToken).ConfigureAwait(false);
                        if (started)
                        {
                            room--;
                        }
                    }
                }

                await gatheringCompleter.TryCompleteAsync(job, forcedReason: null, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }
}
