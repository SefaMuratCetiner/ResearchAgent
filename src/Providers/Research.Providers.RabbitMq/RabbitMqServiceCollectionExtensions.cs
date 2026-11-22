using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Research.Contracts;
using Research.Shared.Adapters.Mediation;
using Research.Shared.Adapters.Messaging;
using Research.Shared.Adapters.Messaging.Consuming;
using Research.Shared.Core.Mediation;
using Research.Shared.Core.Messaging;
using Research.SharedKernel.Results;

namespace Research.Providers.RabbitMq;

/// <summary>
/// The registrations a Host's messaging feature composes, so each Host lists what it publishes and consumes in a few
/// readable lines. The <c>IConnection</c> comes from Aspire (<c>AddRabbitMQClient</c>), registered by the Host.
/// </summary>
public static class RabbitMqServiceCollectionExtensions
{
    /// <summary>Envelopes, messaging metrics and the confirmed publisher.</summary>
    public static void AddPublisher(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<EnvelopeFactory>();
        services.TryAddSingleton<IEnvelopeFactory>(static provider => provider.GetRequiredService<EnvelopeFactory>());
        services.TryAddSingleton<MessagingMetrics>();
        services.TryAddSingleton<RabbitMqMessagePublisher>();
        services.TryAddSingleton<IMessagePublisher>(static provider => provider.GetRequiredService<RabbitMqMessagePublisher>());
    }

    /// <summary>
    /// Consumes <typeparamref name="TMessage"/> with <typeparamref name="THandler"/> (scoped, one per delivery),
    /// wired into the unified mediation pipeline via <see cref="HandleMessage{TMessage}"/>.
    /// </summary>
    public static void AddConsumer<TMessage, THandler>(IServiceCollection services)
        where TMessage : class, IIntegrationMessage
        where THandler : class, IMessageHandler<TMessage>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMediation();
        services.AddScoped<THandler>();
        services.AddScoped<IMessageHandler<TMessage>>(static provider => provider.GetRequiredService<THandler>());
        services.AddScoped<IRequestHandler<HandleMessage<TMessage>, Unit>>(static provider => provider.GetRequiredService<THandler>());
        services.AddSingleton<MessageDispatcher<TMessage>>();
        services.AddHostedService<RabbitMqListener<TMessage>>();
    }
}
