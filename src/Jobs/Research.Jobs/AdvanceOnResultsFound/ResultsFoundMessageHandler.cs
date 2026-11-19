using Microsoft.Extensions.Options;
using Research.Contracts.Fetching;
using Research.Contracts.Search;
using Research.Jobs.Domain;
using Research.Jobs.Domain.Ports;
using Research.Jobs.Lifecycle;
using Research.SharedKernel.Messaging;
using Research.SharedKernel.Security;

namespace Research.Jobs.AdvanceOnResultsFound;

/// <summary>
/// A search answered: record its tokens, start a fetch step per new URL (deduplicated per job by the step key,
/// capped at <see cref="JobsOptions.MaxSources"/>, none once the budget is spent), then check whether gathering ended.
/// </summary>
public sealed class ResultsFoundMessageHandler(
    JobReplyGate gate,
    JobLifecycle lifecycle,
    StepEmitter emitter,
    IJobStepRepository steps,
    IOptionsSnapshot<JobsOptions> options) : IMessageHandler<ResultsFound>
{
    public Task HandleAsync(ResultsFound message, MessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return gate.ProcessAsync(
            message.JobId,
            message.StepKey,
            StepStatus.Succeeded,
            stepErrorCode: null,
            context,
            async (job, transaction) =>
            {
                var current = await lifecycle.AddTokensAsync(job, message.InputTokens + message.OutputTokens, transaction, cancellationToken)
                    .ConfigureAwait(false);
                if (current is null || current.Status != JobStatus.Gathering)
                {
                    return;
                }

                if (!current.BudgetExhausted)
                {
                    var counts = await steps.CountAsync(job.Id, transaction, cancellationToken).ConfigureAwait(false);
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

                        var started = await emitter.EmitAsync(
                            new FetchRequested(job.Id, JobStepKeys.Fetch(hit.Url), hit.Url, UntrustedText.SingleLine(hit.Title)),
                            transaction,
                            cancellationToken).ConfigureAwait(false);
                        if (started)
                        {
                            room--;
                        }
                    }
                }

                await lifecycle.EvaluateGatheringAsync(current, forcedReason: null, transaction, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }
}
