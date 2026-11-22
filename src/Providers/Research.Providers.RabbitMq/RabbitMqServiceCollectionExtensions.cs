using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Research.Shared.Contracts;
using Research.Shared.Adapters.Mediation;
using Research.Shared.Adapters.Messaging;
using Research.Shared.Adapters.Messaging.Consuming;
using Research.Shared.Adapters.Messaging.Outbox;
using Research.Shared.Adapters.Messaging.Sending;
using Research.Shared.Core.Mediation;
using Research.Shared.Messaging;
using Research.Shared.Kernel.Results;
using Research.Shared.Adapters.Messaging.Observability;

namespace Research.Providers.RabbitMq;

/// <summary>
/// The registrations a Host's messaging feature composes, so each Host lists what it publishes and consumes in a few
/// readable lines. The <c>IConnection</c> comes from Aspire (<c>AddRabbitMQClient</c>), registered by the Host.
/// </summary>
public static class RabbitMqServiceCollectionExtensions
{
    /// <summary>Envelopes, messaging metrics and the confirmed publisher.</summary>
    public static IServiceCollection AddPublisher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<EnvelopeFactory>();
        services.TryAddSingleton<IEnvelopeFactory>(static provider => provider.GetRequiredService<EnvelopeFactory>());
        services.TryAddSingleton<MessagingMetrics>();
        services.TryAddSingleton<RabbitMqMessagePublisher>();
        services.TryAddSingleton<IMessagePublisher>(static provider => provider.GetRequiredService<RabbitMqMessagePublisher>());
        return services;
    }

    /// <summary>Confirmed publisher plus the transactional outbox background dispatcher (for DB-backed services).</summary>
    public static IServiceCollection AddOutboxPublisher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddPublisher();
        services.AddHostedService<OutboxPublisherWorker>();
        return services;
    }

    /// <summary>Confirmed publisher plus direct <see cref="IMessageSender"/> (for stateless services without an outbox).</summary>
    public static IServiceCollection AddDirectPublisher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddPublisher();
        services.TryAddSingleton<IMessageSender, DirectMessageSender>();
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
        services.AddMediation();
        services.AddScoped<THandler>();
        services.AddScoped<IMessageHandler<TMessage>>(static provider => provider.GetRequiredService<THandler>());
        services.AddScoped<IRequestHandler<IncomingMessageEnvelope<TMessage>, Unit>>(static provider => provider.GetRequiredService<THandler>());
        services.AddSingleton<MessageDispatcher<TMessage>>();
        services.AddHostedService<RabbitMqListener<TMessage>>();
        return services;
    }
}
