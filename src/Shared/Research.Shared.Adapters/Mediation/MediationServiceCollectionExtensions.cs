using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Research.Shared.Adapters.Mediation.Behaviors;
using Research.Shared.Core.Events;
using Research.Shared.Core.Mediation;
using Research.SharedKernel.Events;
using Research.SharedKernel.Results;

namespace Research.Shared.Adapters.Mediation;

/// <summary>DI registration helpers for the custom mediation pipeline, handlers, validators, and domain event handlers.</summary>
public static class MediationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the unified <see cref="Mediator"/> (<see cref="IMediator"/>, <see cref="ISender"/>,
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
