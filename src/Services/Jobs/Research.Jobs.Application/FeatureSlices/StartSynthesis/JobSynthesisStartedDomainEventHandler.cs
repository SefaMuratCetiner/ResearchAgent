using Microsoft.Extensions.Options;
using Research.Shared.Contracts.Synthesis;
using Research.Jobs.Application.Common.Services;
using Research.Jobs.Domain;
using Research.Jobs.Domain.Events;
using Research.Shared.Core.Events;

namespace Research.Jobs.Application.StartSynthesis;

/// <summary>Emits <c>SynthesisRequested</c> when a job enters Synthesizing.</summary>
public sealed class JobSynthesisStartedDomainEventHandler(
    StepDispatcher stepDispatcher,
    IOptionsSnapshot<JobsOptions> options) : IDomainEventHandler<JobSynthesisStartedDomainEvent>
{
    public Task HandleAsync(JobSynthesisStartedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return stepDispatcher.TryDispatchAsync(
            new SynthesisRequested(domainEvent.JobId, JobStepKeys.Synthesize, domainEvent.Question, domainEvent.Plan, options.Value.SynthesisTopK),
            cancellationToken);
    }
}
