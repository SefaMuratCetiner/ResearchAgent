using Research.Shared.Kernel.Events;

namespace Research.Shared.Bus.Events;

/// <summary>Dispatches uncommitted domain events to all registered <see cref="IDomainEventHandler{TEvent}"/> handlers.</summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken);
}
