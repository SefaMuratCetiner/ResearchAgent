using System.Diagnostics;
using Microsoft.Extensions.Options;
using Research.Contracts;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Domain;
using Research.Shared.Messaging;

namespace Research.Jobs.Application.Common.Services;

/// <summary>
/// Dispatches an outbound worker step within the active unit of work: inserts a leased <see cref="JobStep"/> row
/// (storing the serialized request for timeout re-emission) and enqueues the message in the outbox.
/// </summary>
/// <remarks>
/// Idempotent per <c>(JobId, StepKey)</c>: if the job already has a step with the same key (for example, a duplicate
/// URL across searches or a replayed message), nothing is inserted or enqueued.
/// </remarks>
public sealed class StepDispatcher(
    IJobStepRepository steps,
    IOutbox outbox,
    IEnvelopeFactory envelopes,
    IOptionsSnapshot<JobsOptions> options,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Persists the step and enqueues <paramref name="message"/> if its step key does not already exist for the job.
    /// </summary>
    /// <returns><see langword="true"/> if a new step was created and enqueued; <see langword="false"/> if the step key already existed.</returns>
    public async Task<bool> TryDispatchAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : IIntegrationMessage
    {
        ArgumentNullException.ThrowIfNull(message);
        var now = timeProvider.GetUtcNow();
        var envelope = envelopes.Create(message);

        var step = new JobStep
        {
            Id = Guid.CreateVersion7(now),
            JobId = message.JobId,
            Kind = JobStepKeys.KindOf(message.StepKey),
            StepKey = message.StepKey,
            Status = StepStatus.Running,
            Attempts = 1,
            LeaseUntil = now + options.Value.StepLease,
            TraceId = Activity.Current?.TraceId.ToString(),
            RequestType = envelope.MessageType,
            RequestPayload = envelope.Payload,
        };

        if (!await steps.InsertAsync(step, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await outbox.EnqueueAsync(envelope, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
