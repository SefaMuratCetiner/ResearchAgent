using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Research.Shared.Bus;
using Research.Shared.Bus.Messaging;
using Research.Shared.Contracts;

namespace Research.Providers.RabbitMq;

/// <summary>
/// The registrations a Host's messaging feature composes, so each Host lists what it publishes and consumes in a few
/// readable lines. The <c>IConnection</c> comes from Aspire (<c>AddRabbitMQClient</c>), registered by the Host.
/// </summary>
public static class RabbitMqServiceCollectionExtensions
{
    /// <summary>Envelopes, messaging metrics and the confirmed RabbitMQ publisher.</summary>
    public static IServiceCollection AddPublisher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMessagingCore();
        services.TryAddSingleton<RabbitMqMessagePublisher>();
        services.TryAddSingleton<IMessagePublisher>(static provider => provider.GetRequiredService<RabbitMqMessagePublisher>());
        return services;
    }

    /// <summary>Confirmed publisher plus the transactional outbox background dispatcher (for DB-backed services).</summary>
    public static IServiceCollection AddOutboxPublisher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddPublisher();
        services.AddOutboxWorker();
        return services;
    }

    /// <summary>Confirmed publisher plus direct <see cref="IMessageSender"/> (for stateless services without an outbox).</summary>
    public static IServiceCollection AddDirectPublisher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddPublisher();
        services.AddDirectMessageSender();
        return services;
    }

    /// <summary>
    /// Consumes <typeparamref name="TMessage"/> with <typeparamref name="THandler"/> (scoped, one per delivery),
    /// wired into the unified mediation pipeline via <see cref="IncomingMessageEnvelope{TMessage}"/>.
    /// </summary>
    public static IServiceCollection AddConsumer<TMessage, THandler>(this IServiceCollection services)
        where TMessage : class, IIntegrationMessage
        where THandler : class, IMessageHandler<TMessage>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMessageConsumer<TMessage, THandler>();
        services.AddHostedService<RabbitMqConsumerWorker<TMessage>>();
        return services;
    }
}
