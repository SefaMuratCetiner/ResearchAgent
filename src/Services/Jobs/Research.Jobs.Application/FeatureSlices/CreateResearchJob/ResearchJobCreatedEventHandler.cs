using Microsoft.Extensions.Options;
using Research.Jobs.Application.Common.Services;
using Research.Jobs.Application.Observability;
using Research.Jobs.Domain;
using Research.Jobs.Domain.Events;
using Research.Shared.Bus.Events;
using Research.Shared.Bus.Messaging;
using Research.Shared.Contracts.Jobs;
using Research.Shared.Contracts.Planning;

namespace Research.Jobs.Application.CreateResearchJob;

/// <summary>Starts the plan step (<c>PlanRequested</c>) and enqueues <c>JobCreated</c> when a job is created.</summary>
public sealed class ResearchJobCreatedEventHandler(
    StepDispatcher stepDispatcher,
    IOutbox outbox,
    IEnvelopeFactory envelopes,
    JobsMetrics metrics,
    IOptionsSnapshot<JobsOptions> options) : IDomainEventHandler<ResearchJobCreatedDomainEvent>
{
    public async Task HandleAsync(ResearchJobCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        await stepDispatcher.TryDispatchAsync(
            new PlanRequested(domainEvent.JobId, JobStepKeys.Plan, domainEvent.Question, options.Value.MaxSubQuestions),
            cancellationToken).ConfigureAwait(false);
        await outbox.EnqueueAsync(
            envelopes.Create(new JobCreated(domainEvent.JobId, JobTerminationService.JobStepKey, domainEvent.Question)),
            cancellationToken).ConfigureAwait(false);
        metrics.Created();
    }
}
