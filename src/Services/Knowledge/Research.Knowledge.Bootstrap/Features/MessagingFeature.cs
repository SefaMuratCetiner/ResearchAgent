using Research.Contracts.Fetching;
using Research.Knowledge.Application.IndexDocument;
using Research.Providers.RabbitMq;
using Research.Shared.Adapters.Composition;

namespace Research.Knowledge.Bootstrap.Features;

/// <summary>
/// RabbitMQ (Aspire connection <c>bus</c>): consumes <c>DocumentFetched</c>; replies go through the outbox and its
/// dispatcher.
/// </summary>
internal sealed class MessagingFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Host.AddRabbitMQClient("bus");
        app.Services.AddOutboxPublisher();
        app.Services.AddConsumer<DocumentFetched, DocumentFetchedMessageHandler>();
    }
}
