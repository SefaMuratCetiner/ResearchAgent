using Research.SharedKernel.Events;

namespace Research.Shared.Core.Events;

/// <summary>
/// Handles a domain event raised by an aggregate root, running inside the active <see cref="Transactions.IUnitOfWork"/>
/// transaction right before commit so outbox messages and follow-up work commit atomically with the state change.
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
