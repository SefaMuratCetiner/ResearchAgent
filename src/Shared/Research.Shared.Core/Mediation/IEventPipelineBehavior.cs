using Research.Shared.Kernel.Events;

namespace Research.Shared.Core.Mediation;

/// <summary>Chain-of-responsibility middleware wrapping every <see cref="IPublisher.PublishAsync{TEvent}"/> execution.</summary>
public interface IEventPipelineBehavior<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(
        TEvent domainEvent,
        EventHandlerDelegate next,
        CancellationToken cancellationToken);
}
