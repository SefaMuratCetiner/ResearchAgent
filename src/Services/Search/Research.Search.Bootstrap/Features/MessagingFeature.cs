using Research.Contracts.Search;
using Research.ServiceDefaults.Composition;
using Research.Shared.Core.Messaging;
using Research.Providers.RabbitMq;
using Research.Search.RunSearch;

namespace Research.Search.Bootstrap.Features;

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
        app.Services.AddSingleton<IMessageSender, Research.Shared.Adapters.Messaging.Sending.DirectMessageSender>();

        RabbitMqServiceCollectionExtensions.AddConsumer<SearchRequested, SearchRequestedMessageHandler>(app.Services);
    }
}
