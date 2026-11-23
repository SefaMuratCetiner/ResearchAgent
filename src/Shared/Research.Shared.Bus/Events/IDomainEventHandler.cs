using Research.Shared.Kernel.Events;

namespace Research.Shared.Bus.Events;

/// <summary>
/// Handles a domain event raised by an aggregate root. Events are published when the aggregate is saved, inside the
/// active <see cref="Transactions.IUnitOfWork"/> transaction, so outbox messages and follow-up work commit atomically
/// with the state change.
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
