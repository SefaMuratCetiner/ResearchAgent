using Research.Shared.Contracts.Fetching;
using Research.Fetching.Application.FetchPage;
using Research.Providers.RabbitMq;
using Research.Shared.Core.Hosting;

namespace Research.Fetching.Bootstrap.Features;

/// <summary>
/// RabbitMQ (Aspire connection <c>bus</c>): consumes <c>FetchRequested</c>; replies go through the outbox and its
/// dispatcher.
/// </summary>
internal sealed class MessagingFeature : IHostFeature
{
    public void Register(ServiceHostBuilder app)
    {
        app.Host.AddRabbitMQClient("bus");
        app.Services.AddOutboxPublisher();
        app.Services.AddConsumer<FetchRequested, FetchRequestedMessageHandler>();
    }
}
