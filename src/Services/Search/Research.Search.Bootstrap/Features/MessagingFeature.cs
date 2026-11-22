using Research.Shared.Contracts.Search;
using Research.Providers.RabbitMq;
using Research.Search.Application.RunSearch;
using Research.Shared.Adapters.Composition;

namespace Research.Search.Bootstrap.Features;

/// <summary>
/// RabbitMQ (Aspire connection <c>bus</c>): consumes <c>SearchRequested</c> and replies directly with confirmed
/// publishes (no database, no outbox).
/// </summary>
internal sealed class MessagingFeature : IHostFeature
{
    public void Register(ServiceHostBuilder app)
    {
        app.Host.AddRabbitMQClient("bus");
        app.Services.AddDirectPublisher();
        app.Services.AddConsumer<SearchRequested, SearchRequestedMessageHandler>();
    }
}
