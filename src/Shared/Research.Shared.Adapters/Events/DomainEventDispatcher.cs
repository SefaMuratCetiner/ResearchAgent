using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Research.Shared.Core.Events;
using Research.SharedKernel.Common;

namespace Research.Shared.Adapters.Events;

/// <summary>
/// Lightweight, zero-commercial-dependency domain event dispatcher that resolves all registered
/// <see cref="IDomainEventHandler{TEvent}"/> handlers from the current DI scope.
/// </summary>
public sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    private static readonly MethodInfo DispatchGenericMethod = typeof(DomainEventDispatcher)
        .GetMethod(nameof(DispatchTypedAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, IDomainEvent, CancellationToken, Task>> Dispatchers = new();

    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (var domainEvent in domainEvents)
        {
            var invoke = Dispatchers.GetOrAdd(domainEvent.GetType(), CreateInvoker);
            await invoke(serviceProvider, domainEvent, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Func<IServiceProvider, IDomainEvent, CancellationToken, Task> CreateInvoker(Type eventType)
    {
        var method = DispatchGenericMethod.MakeGenericMethod(eventType);
        return (sp, evt, ct) => (Task)method.Invoke(null, [sp, evt, ct])!;
    }

    private static async Task DispatchTypedAsync<TEvent>(
        IServiceProvider serviceProvider,
        IDomainEvent domainEvent,
        CancellationToken cancellationToken)
        where TEvent : IDomainEvent
    {
        var typedEvent = (TEvent)domainEvent;
        foreach (var handler in serviceProvider.GetServices<IDomainEventHandler<TEvent>>())
        {
            await handler.HandleAsync(typedEvent, cancellationToken).ConfigureAwait(false);
        }
    }
}
