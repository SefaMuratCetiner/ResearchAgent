using Microsoft.Extensions.Options;
using Research.Jobs.Application.Common.Services;
using Research.Jobs.Domain;
using Research.Jobs.Domain.Events;
using Research.Shared.Bus.Events;
using Research.Shared.Contracts.Synthesis;

namespace Research.Jobs.Application.StartSynthesis;

/// <summary>Starts the synthesis step (<c>SynthesisRequested</c>) when a job enters Synthesizing.</summary>
public sealed class SynthesisStartedEventHandler(
    StepDispatcher stepDispatcher,
    IOptionsSnapshot<JobsOptions> options) : IDomainEventHandler<SynthesisStartedDomainEvent>
{
    public Task HandleAsync(SynthesisStartedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return stepDispatcher.TryDispatchAsync(
            new SynthesisRequested(domainEvent.JobId, JobStepKeys.Synthesize, domainEvent.Question, domainEvent.Plan, options.Value.SynthesisTopK),
            cancellationToken);
    }
}
