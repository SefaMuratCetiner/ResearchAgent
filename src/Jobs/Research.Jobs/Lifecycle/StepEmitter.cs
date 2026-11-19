using System.Diagnostics;
using Microsoft.Extensions.Options;
using Research.Contracts;
using Research.Jobs.Domain;
using Research.Jobs.Domain.Ports;
using Research.Messaging;
using Research.SharedKernel.Actors;
using Research.SharedKernel.Messaging;
using Research.SharedKernel.Transactions;

namespace Research.Jobs.Lifecycle;

/// <summary>
/// Starts a step: inserts its row (with the request, for re-emission, and a lease) and enqueues the request in the
/// outbox, in the caller's transaction. A step key the job already has is skipped, which is how duplicate URLs and
/// replayed replies collapse.
/// </summary>
public sealed class StepEmitter(
    IJobStepRepository steps,
    IOutbox outbox,
    EnvelopeFactory envelopes,
    IOptionsSnapshot<JobsOptions> options,
    TimeProvider timeProvider,
    ICurrentActor actor)
{
    /// <returns>True when the step was new and its request enqueued.</returns>
    public async Task<bool> EmitAsync<TMessage>(TMessage message, ITransactionScope transaction, CancellationToken cancellationToken)
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
            CreatedAt = now,
            CreatedBy = actor.Id,
        };

        if (!await steps.InsertAsync(step, transaction, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await outbox.EnqueueAsync(envelope, transaction, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
