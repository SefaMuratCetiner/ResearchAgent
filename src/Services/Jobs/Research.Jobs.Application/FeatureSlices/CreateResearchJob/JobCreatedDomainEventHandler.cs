using Microsoft.Extensions.Options;
using Research.Contracts.Jobs;
using Research.Contracts.Planning;
using Research.Jobs.Application.Common.Services;
using Research.Jobs.Application.Observability;
using Research.Jobs.Domain;
using Research.Jobs.Domain.Events;
using Research.Shared.Core.Events;
using Research.Shared.Messaging;

namespace Research.Jobs.Application.CreateResearchJob;

/// <summary>Emits the initial <c>PlanRequested</c> step and enqueues <c>JobCreated</c> when a job is created.</summary>
public sealed class JobCreatedDomainEventHandler(
    StepDispatcher stepDispatcher,
    IOutbox outbox,
    IEnvelopeFactory envelopes,
    JobsMetrics metrics,
    IOptionsSnapshot<JobsOptions> options) : IDomainEventHandler<JobCreatedDomainEvent>
{
    private const string JobStepKey = "job";

    public async Task HandleAsync(JobCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        await stepDispatcher.TryDispatchAsync(
            new PlanRequested(domainEvent.JobId, JobStepKeys.Plan, domainEvent.Question, options.Value.MaxSubQuestions),
            cancellationToken).ConfigureAwait(false);
        await outbox.EnqueueAsync(
            envelopes.Create(new JobCreated(domainEvent.JobId, JobStepKey, domainEvent.Question)),
            cancellationToken).ConfigureAwait(false);
        metrics.Created();
    }
}
