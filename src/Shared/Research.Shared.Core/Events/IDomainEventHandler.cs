using Research.Shared.Core.Transactions;
using Research.SharedKernel.Common;

namespace Research.Shared.Core.Events;

/// <summary>
/// Handles a domain event raised by an aggregate root, running inside the active transaction right before commit so
/// outbox messages and follow-up work commit atomically with the aggregate's state change.
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, ITransactionScope transaction, CancellationToken cancellationToken);
}
