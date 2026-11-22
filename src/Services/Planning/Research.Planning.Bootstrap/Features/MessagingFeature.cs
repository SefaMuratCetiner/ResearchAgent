using Research.Shared.Contracts.Planning;
using Research.Planning.Application.CreatePlan;
using Research.Providers.RabbitMq;
using Research.Shared.Adapters.Composition;

namespace Research.Planning.Bootstrap.Features;

/// <summary>
/// RabbitMQ (Aspire connection <c>bus</c>): consumes <c>PlanRequested</c> and replies directly with confirmed publishes
/// (no database, no outbox).
/// </summary>
internal sealed class MessagingFeature : IHostFeature
{
    public void Register(ServiceHostBuilder app)
    {
        app.Host.AddRabbitMQClient("bus");
        app.Services.AddDirectPublisher();
        app.Services.AddConsumer<PlanRequested, PlanRequestedMessageHandler>();
    }
}
