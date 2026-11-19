using Research.Contracts.Planning;
using Research.Hosting.Composition;
using Research.Messaging.Sending;
using Research.Planning.CreatePlan;
using Research.Providers.RabbitMq;

namespace Research.Planning.Host.Features;

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
        app.Services.AddSingleton<IMessageSender, DirectMessageSender>();

        RabbitMqServiceCollectionExtensions.AddConsumer<PlanRequested, PlanRequestedMessageHandler>(app.Services);
    }
}
