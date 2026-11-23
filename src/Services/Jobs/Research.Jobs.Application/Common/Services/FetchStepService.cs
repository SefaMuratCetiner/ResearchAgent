using Microsoft.Extensions.Options;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Domain;
using Research.Shared.Contracts.Fetching;
using Research.Shared.Contracts.Search;
using Research.Shared.Core.Security;

namespace Research.Jobs.Application.Common.Services;

/// <summary>
/// Starts one <c>FetchRequested</c> step per new search hit: deduplicated per job by the step key, capped at
/// <see cref="JobsOptions.MaxSources"/>, and none once the job's token budget is spent.
/// </summary>
public sealed class FetchStepService(
    StepDispatcher stepDispatcher,
    IJobStepRepository steps,
    IOptionsSnapshot<JobsOptions> options)
{
    public async Task StartFetchesAsync(ResearchJob job, IReadOnlyList<SearchResultItem>? hits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.BudgetExhausted)
        {
            return;
        }

        var counts = await steps.CountAsync(job.Id, cancellationToken).ConfigureAwait(false);
        var room = options.Value.MaxSources - counts.FetchTotal;
        foreach (var hit in hits ?? [])
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
}
