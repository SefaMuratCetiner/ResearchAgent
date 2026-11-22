using Research.Contracts.Planning;
using Research.Shared.Adapters.Composition;
using Research.Shared.Messaging;
using Research.Planning.Application.CreatePlan;
using Research.Providers.RabbitMq;

namespace Research.Planning.Bootstrap.Features;

/// <summary>
/// RabbitMQ (Aspire connection <c>bus</c>): consumes <c>PlanRequested</c> and replies directly with confirmed publishes
/// (no database, no outbox).
/// </summary>
internal sealed class MessagingFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Host.AddRabbitMQClient("bus");

        RabbitMqServiceCollectionExtensions.AddPublisher(app.Services);
        app.Services.AddSingleton<IMessageSender, Research.Shared.Adapters.Messaging.Sending.DirectMessageSender>();

        RabbitMqServiceCollectionExtensions.AddConsumer<PlanRequested, PlanRequestedMessageHandler>(app.Services);
    }
}
