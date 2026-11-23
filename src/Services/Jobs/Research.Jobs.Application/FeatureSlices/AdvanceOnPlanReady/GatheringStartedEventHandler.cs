using Microsoft.Extensions.Options;
using Research.Jobs.Application.Common.Services;
using Research.Jobs.Domain;
using Research.Jobs.Domain.Events;
using Research.Shared.Bus.Events;
using Research.Shared.Contracts.Search;

namespace Research.Jobs.Application.AdvanceOnPlanReady;

/// <summary>Starts one <c>SearchRequested</c> step per sub-question when a job enters Gathering.</summary>
public sealed class GatheringStartedEventHandler(
    StepDispatcher stepDispatcher,
    IOptionsSnapshot<JobsOptions> options) : IDomainEventHandler<GatheringStartedDomainEvent>
{
    public async Task HandleAsync(GatheringStartedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var resultsPerSearch = options.Value.ResultsPerSearch;
        for (var i = 0; i < domainEvent.SubQuestions.Count; i++)
        {
            await stepDispatcher.TryDispatchAsync(
                new SearchRequested(domainEvent.JobId, JobStepKeys.Search(i), domainEvent.SubQuestions[i], resultsPerSearch),
                cancellationToken).ConfigureAwait(false);
        }
    }
}
