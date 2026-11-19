using Research.Contracts.Synthesis;
using Research.Hosting.Composition;
using Research.Messaging.Sending;
using Research.Providers.RabbitMq;
using Research.Synthesis.WriteReport;

namespace Research.Synthesis.Host.Features;

/// <summary>
/// RabbitMQ (Aspire connection <c>bus</c>): consumes <c>SynthesisRequested</c> and replies directly with confirmed
/// publishes (no database, no outbox).
/// </summary>
internal sealed class MessagingFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Host.AddRabbitMQClient("bus");

        RabbitMqServiceCollectionExtensions.AddPublisher(app.Services);
        app.Services.AddSingleton<IMessageSender, DirectMessageSender>();

        RabbitMqServiceCollectionExtensions.AddConsumer<SynthesisRequested, SynthesisRequestedMessageHandler>(app.Services);
    }
}
