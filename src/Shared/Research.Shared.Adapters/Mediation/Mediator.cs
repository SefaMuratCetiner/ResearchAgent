using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Research.Shared.Core.Events;
using Research.Shared.Core.Mediation;
using Research.Shared.Kernel.Events;
using Research.Shared.Kernel.Results;

namespace Research.Shared.Adapters.Mediation;

/// <summary>
/// Custom, zero-third-party-package mediator unifying commands, queries, integration message handling, and domain event
/// publishing through a single Chain-of-Responsibility pipeline (<see cref="IPipelineBehavior{TRequest, TResponse}"/> and
/// <see cref="IEventPipelineBehavior{TEvent}"/>). Uses cached strongly-typed handler wrappers to avoid runtime reflection
/// on the hot path and preserve original exception stack traces.
/// </summary>
public sealed class Mediator(IServiceProvider serviceProvider) : IMediator, IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> RequestWrappers = new();
    private static readonly ConcurrentDictionary<Type, EventHandlerWrapper> EventWrappers = new();

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = (RequestHandlerWrapper<TResponse>)RequestWrappers.GetOrAdd(
            request.GetType(),
            static requestType =>
            {
                var wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, typeof(TResponse));
                return Activator.CreateInstance(wrapperType)
                    ?? throw new InvalidOperationException($"Could not create request handler wrapper for {requestType.Name}.");
            });

        return wrapper.HandleAsync(request, serviceProvider, cancellationToken);
    }

    public async Task SendAsync(ICommand command, CancellationToken cancellationToken) =>
        _ = await SendAsync<Unit>(command, cancellationToken).ConfigureAwait(false);

    public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return EventHandlerWrapperImpl<TEvent>.DispatchAsync(domainEvent, serviceProvider, cancellationToken);
    }

    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var wrapper = EventWrappers.GetOrAdd(
            domainEvent.GetType(),
            static eventType =>
            {
                var wrapperType = typeof(EventHandlerWrapperImpl<>).MakeGenericType(eventType);
                return (EventHandlerWrapper)(Activator.CreateInstance(wrapperType)
                    ?? throw new InvalidOperationException($"Could not create event handler wrapper for {eventType.Name}."));
            });

        return wrapper.PublishAsync(domainEvent, serviceProvider, cancellationToken);
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (var domainEvent in domainEvents)
        {
            await PublishAsync(domainEvent, cancellationToken).ConfigureAwait(false);
        }
    }

    private abstract class RequestHandlerWrapper<TResponse>
    {
        public abstract Task<TResponse> HandleAsync(
            IRequest<TResponse> request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken);
    }

    private sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
        where TRequest : IRequest<TResponse>
    {
        public override Task<TResponse> HandleAsync(
            IRequest<TResponse> request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
        {
            var typedRequest = (TRequest)request;
            var handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

            RequestHandlerDelegate<TResponse> pipeline = ct => handler.HandleAsync(typedRequest, ct);

            // Build the Chain of Responsibility from innermost to outermost so behaviors execute in registration order.
            var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
            if (behaviors is IList<IPipelineBehavior<TRequest, TResponse>> list)
            {
                for (var i = list.Count - 1; i >= 0; i--)
                {
                    var behavior = list[i];
                    var next = pipeline;
                    pipeline = ct => behavior.HandleAsync(typedRequest, next, ct);
                }
            }
            else
            {
                foreach (var behavior in behaviors.Reverse())
                {
                    var next = pipeline;
                    pipeline = ct => behavior.HandleAsync(typedRequest, next, ct);
                }
            }

            return pipeline(cancellationToken);
        }
    }

    private abstract class EventHandlerWrapper
    {
        public abstract Task PublishAsync(
            IDomainEvent domainEvent,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken);
    }

    private sealed class EventHandlerWrapperImpl<TEvent> : EventHandlerWrapper
        where TEvent : IDomainEvent
    {
        public override Task PublishAsync(
            IDomainEvent domainEvent,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken) =>
            DispatchAsync((TEvent)domainEvent, serviceProvider, cancellationToken);

        public static Task DispatchAsync(
            TEvent domainEvent,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
        {
            EventHandlerDelegate pipeline = async ct =>
            {
                foreach (var handler in serviceProvider.GetServices<IDomainEventHandler<TEvent>>())
                {
                    await handler.HandleAsync(domainEvent, ct).ConfigureAwait(false);
                }
            };

            var behaviors = serviceProvider.GetServices<IEventPipelineBehavior<TEvent>>();
            if (behaviors is IList<IEventPipelineBehavior<TEvent>> list)
            {
                for (var i = list.Count - 1; i >= 0; i--)
                {
                    var behavior = list[i];
                    var next = pipeline;
                    pipeline = ct => behavior.HandleAsync(domainEvent, next, ct);
                }
            }
            else
            {
                foreach (var behavior in behaviors.Reverse())
                {
                    var next = pipeline;
                    pipeline = ct => behavior.HandleAsync(domainEvent, next, ct);
                }
            }

            return pipeline(cancellationToken);
        }
    }
}
