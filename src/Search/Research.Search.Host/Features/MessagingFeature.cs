using Research.Contracts.Search;
using Research.Hosting.Composition;
using Research.Messaging.Sending;
using Research.Providers.RabbitMq;
using Research.Search.RunSearch;

namespace Research.Search.Host.Features;

/// <summary>
/// RabbitMQ (Aspire connection <c>bus</c>): consumes <c>SearchRequested</c> and replies directly with confirmed
/// publishes (no database, no outbox).
/// </summary>
internal sealed class MessagingFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Host.AddRabbitMQClient("bus");

        RabbitMqServiceCollectionExtensions.AddPublisher(app.Services);
        app.Services.AddSingleton<IMessageSender, DirectMessageSender>();

        RabbitMqServiceCollectionExtensions.AddConsumer<SearchRequested, SearchRequestedMessageHandler>(app.Services);
    }
}
