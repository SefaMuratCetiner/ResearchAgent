using Research.Contracts.Fetching;
using Research.Fetching.FetchPage;
using Research.ServiceDefaults.Composition;
using Research.Shared.Adapters.Messaging.Outbox;
using Research.Providers.RabbitMq;

namespace Research.Fetching.Bootstrap.Features;

/// <summary>
/// RabbitMQ (Aspire connection <c>bus</c>): consumes <c>FetchRequested</c>; replies go through the outbox and its
/// dispatcher.
/// </summary>
internal sealed class MessagingFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Host.AddRabbitMQClient("bus");

        RabbitMqServiceCollectionExtensions.AddPublisher(app.Services);
        app.Services.AddHostedService<OutboxBackgroundService>();

        RabbitMqServiceCollectionExtensions.AddConsumer<FetchRequested, FetchRequestedMessageHandler>(app.Services);
    }
}
