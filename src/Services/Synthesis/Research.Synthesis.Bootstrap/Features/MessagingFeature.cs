using Research.Contracts.Synthesis;
using Research.Shared.Adapters.Composition;
using Research.Shared.Core.Messaging;
using Research.Providers.RabbitMq;
using Research.Synthesis.Application.WriteReport;

namespace Research.Synthesis.Bootstrap.Features;

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
        app.Services.AddSingleton<IMessageSender, Research.Shared.Adapters.Messaging.Sending.DirectMessageSender>();

        RabbitMqServiceCollectionExtensions.AddConsumer<SynthesisRequested, SynthesisRequestedMessageHandler>(app.Services);
    }
}
