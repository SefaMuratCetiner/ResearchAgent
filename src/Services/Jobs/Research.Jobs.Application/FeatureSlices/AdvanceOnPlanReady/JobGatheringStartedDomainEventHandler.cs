using Microsoft.Extensions.Options;
using Research.Contracts.Search;
using Research.Jobs.Application.Lifecycle;
using Research.Jobs.Domain;
using Research.Jobs.Domain.Events;
using Research.Shared.Core.Events;

namespace Research.Jobs.Application.AdvanceOnPlanReady;

/// <summary>Emits one <c>SearchRequested</c> step per sub-question when a job enters Gathering.</summary>
public sealed class JobGatheringStartedDomainEventHandler(
    StepEmitter emitter,
    IOptionsSnapshot<JobsOptions> options) : IDomainEventHandler<JobGatheringStartedDomainEvent>
{
    public async Task HandleAsync(JobGatheringStartedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var resultsPerSearch = options.Value.ResultsPerSearch;
        for (var i = 0; i < domainEvent.SubQuestions.Count; i++)
        {
            await emitter.EmitAsync(
                new SearchRequested(domainEvent.JobId, JobStepKeys.Search(i), domainEvent.SubQuestions[i], resultsPerSearch),
                cancellationToken).ConfigureAwait(false);
        }
    }
}
