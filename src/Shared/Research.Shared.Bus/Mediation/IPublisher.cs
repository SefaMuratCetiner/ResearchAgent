using Research.Shared.Kernel.Events;

namespace Research.Shared.Bus.Mediation;

/// <summary>Publishes a domain event through the event pipeline to all registered <see cref="Events.IDomainEventHandler{TEvent}"/>s.</summary>
public interface IPublisher
{
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : IDomainEvent;

    Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken);
}
