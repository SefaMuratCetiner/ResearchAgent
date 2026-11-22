using Research.SharedKernel.Events;

namespace Research.Shared.Core.Mediation;

/// <summary>Dispatches a command, query, or integration message request through the configured pipeline to its single handler.</summary>
public interface ISender
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken);

    Task SendAsync(ICommand command, CancellationToken cancellationToken);
}

/// <summary>Publishes a domain event through the event pipeline to all registered <see cref="Events.IDomainEventHandler{TEvent}"/>s.</summary>
public interface IPublisher
{
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : IDomainEvent;

    Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken);
}

/// <summary>Unified in-process mediator covering commands, queries, integration messages, and domain events.</summary>
public interface IMediator : ISender, IPublisher;
