using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Research.Shared.Bus.Events;
using Research.Shared.Bus.Events.Behaviors;
using Research.Shared.Bus.Mediation;
using Research.Shared.Bus.Mediation.Behaviors;
using Research.Shared.Bus.Mediation.Observability;
using Research.Shared.Bus.Messaging;
using Research.Shared.Bus.Messaging.Consuming;
using Research.Shared.Bus.Messaging.Observability;
using Research.Shared.Bus.Messaging.Outbox;
using Research.Shared.Bus.Messaging.Sending;
using Research.Shared.Bus.Transactions;
using Research.Shared.Contracts;
using Research.Shared.Kernel.Events;
using Research.Shared.Kernel.Results;

namespace Research.Shared.Bus;

/// <summary>DI registration helpers for the unified mediation pipeline, domain events, and integration messaging.</summary>
public static class BusServiceCollectionExtensions
{
    /// <summary>
    /// Registers the unified mediator (<see cref="IMediator"/>, <see cref="ISender"/>,
    /// <see cref="IPublisher"/>, <see cref="IDomainEventDispatcher"/>) and the standard cross-cutting pipeline behaviors
    /// in order: Telemetry → Logging → Inbox → Validation → Transaction.
    /// </summary>
    public static IServiceCollection AddMediation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<MediationMetrics>();

        services.TryAddScoped<Mediator>();
        services.TryAddScoped<IMediator>(static sp => sp.GetRequiredService<Mediator>());
        services.TryAddScoped<ISender>(static sp => sp.GetRequiredService<Mediator>());
        services.TryAddScoped<IPublisher>(static sp => sp.GetRequiredService<Mediator>());
        services.TryAddScoped<IDomainEventDispatcher>(static sp => sp.GetRequiredService<Mediator>());

        // Request pipeline (outermost to innermost):
        // 1. Telemetry → 2. Logging → 3. Inbox → 4. Validation → 5. Transaction → Handler
        // Telemetry sits outside Logging so mediation logs carry the mediation span id.
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IPipelineBehavior<,>), typeof(TelemetryBehavior<,>)));
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>)));
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IPipelineBehavior<,>), typeof(InboxBehavior<,>)));
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>)));
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>)));

        // Domain event pipeline (outermost to innermost):
        // 1. Telemetry → 2. Logging → 0..N Domain Event Handlers
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IEventPipelineBehavior<>), typeof(EventTelemetryBehavior<>)));
        services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IEventPipelineBehavior<>), typeof(EventLoggingBehavior<>)));

        return services;
    }

    /// <summary>Registers broker-independent envelope creation and messaging metrics.</summary>
    public static IServiceCollection AddMessagingCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<EnvelopeFactory>();
        services.TryAddSingleton<IEnvelopeFactory>(static sp => sp.GetRequiredService<EnvelopeFactory>());
        services.TryAddSingleton<MessagingMetrics>();
        return services;
    }

    /// <summary>Registers the transactional outbox background publisher worker (for DB-backed services).</summary>
    public static IServiceCollection AddOutboxWorker(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMessagingCore();
        services.AddHostedService<OutboxPublisherWorker>();
        return services;
    }

    /// <summary>Registers the direct <see cref="IMessageSender"/> (for stateless services without an outbox).</summary>
    public static IServiceCollection AddDirectMessageSender(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMessagingCore();
        services.TryAddSingleton<IMessageSender, DirectMessageSender>();
        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="THandler"/> and the broker-independent <see cref="IMessageDispatcher{TMessage}"/>
    /// for <typeparamref name="TMessage"/> inside the unified mediation pipeline.
    /// </summary>
    public static IServiceCollection AddMessageConsumer<TMessage, THandler>(this IServiceCollection services)
        where TMessage : class, IIntegrationMessage
        where THandler : class, IMessageHandler<TMessage>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMediation();
        services.AddMessagingCore();
        services.AddScoped<THandler>();
        services.AddScoped<IMessageHandler<TMessage>>(static sp => sp.GetRequiredService<THandler>());
        services.AddScoped<IRequestHandler<IncomingMessageEnvelope<TMessage>, Unit>>(static sp => sp.GetRequiredService<THandler>());
        services.TryAddSingleton<IMessageDispatcher<TMessage>, MessageDispatcher<TMessage>>();
        return services;
    }

    public static IServiceCollection AddCommandHandler<TCommand, TResponse, THandler>(this IServiceCollection services)
        where TCommand : ICommand<TResponse>
        where THandler : class, ICommandHandler<TCommand, TResponse>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IRequestHandler<TCommand, TResponse>, THandler>();
        return services;
    }

    public static IServiceCollection AddCommandHandler<TCommand, THandler>(this IServiceCollection services)
        where TCommand : ICommand
        where THandler : class, ICommandHandler<TCommand>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IRequestHandler<TCommand, Unit>, THandler>();
        return services;
    }

    public static IServiceCollection AddQueryHandler<TQuery, TResponse, THandler>(this IServiceCollection services)
        where TQuery : IQuery<TResponse>
        where THandler : class, IQueryHandler<TQuery, TResponse>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IRequestHandler<TQuery, TResponse>, THandler>();
        return services;
    }

    public static IServiceCollection AddRequestValidator<TRequest, TValidator>(this IServiceCollection services)
        where TValidator : class, IRequestValidator<TRequest>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IRequestValidator<TRequest>, TValidator>();
        return services;
    }

    public static IServiceCollection AddDomainEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IDomainEvent
        where THandler : class, IDomainEventHandler<TEvent>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IDomainEventHandler<TEvent>, THandler>();
        return services;
    }
}
