using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Research.Shared.Core.Events;
using Research.Shared.Core.Transactions;
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

    private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, IDomainEvent, ITransactionScope, CancellationToken, Task>> Dispatchers = new();

    public async Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        ITransactionScope transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);
        ArgumentNullException.ThrowIfNull(transaction);

        foreach (var domainEvent in domainEvents)
        {
            var invoke = Dispatchers.GetOrAdd(domainEvent.GetType(), CreateInvoker);
            await invoke(serviceProvider, domainEvent, transaction, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Func<IServiceProvider, IDomainEvent, ITransactionScope, CancellationToken, Task> CreateInvoker(Type eventType)
    {
        var method = DispatchGenericMethod.MakeGenericMethod(eventType);
        return (sp, evt, tx, ct) => (Task)method.Invoke(null, [sp, evt, tx, ct])!;
    }

    private static async Task DispatchTypedAsync<TEvent>(
        IServiceProvider serviceProvider,
        IDomainEvent domainEvent,
        ITransactionScope transaction,
        CancellationToken cancellationToken)
        where TEvent : IDomainEvent
    {
        var typedEvent = (TEvent)domainEvent;
        foreach (var handler in serviceProvider.GetServices<IDomainEventHandler<TEvent>>())
        {
            await handler.HandleAsync(typedEvent, transaction, cancellationToken).ConfigureAwait(false);
        }
    }
}
