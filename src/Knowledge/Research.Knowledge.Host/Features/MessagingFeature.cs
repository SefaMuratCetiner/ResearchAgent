using Research.Contracts.Fetching;
using Research.Hosting.Composition;
using Research.Knowledge.IndexDocument;
using Research.Messaging.Outbox;
using Research.Providers.RabbitMq;

namespace Research.Knowledge.Host.Features;

/// <summary>
/// RabbitMQ (Aspire connection <c>bus</c>): consumes <c>DocumentFetched</c>; replies go through the outbox and its
/// dispatcher.
/// </summary>
internal sealed class MessagingFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Host.AddRabbitMQClient("bus");

        RabbitMqServiceCollectionExtensions.AddPublisher(app.Services);
        app.Services.AddHostedService<OutboxBackgroundService>();

        RabbitMqServiceCollectionExtensions.AddConsumer<DocumentFetched, DocumentFetchedMessageHandler>(app.Services);
    }
}
