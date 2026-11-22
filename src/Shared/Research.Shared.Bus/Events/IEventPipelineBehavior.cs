using Research.Shared.Bus.Mediation;
using Research.Shared.Kernel.Events;

namespace Research.Shared.Bus.Events;

/// <summary>Chain-of-responsibility middleware wrapping every <see cref="IPublisher.PublishAsync{TEvent}"/> execution.</summary>
public interface IEventPipelineBehavior<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(
        TEvent domainEvent,
        EventHandlerDelegate next,
        CancellationToken cancellationToken);
}
