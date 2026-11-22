using Research.Shared.Contracts.Synthesis;
using Research.Providers.RabbitMq;
using Research.Shared.Core.Hosting;
using Research.Synthesis.Application.WriteReport;

namespace Research.Synthesis.Bootstrap.Features;

/// <summary>
/// RabbitMQ (Aspire connection <c>bus</c>): consumes <c>SynthesisRequested</c> and replies directly with confirmed
/// publishes (no database, no outbox).
/// </summary>
internal sealed class MessagingFeature : IHostFeature
{
    public void Register(ServiceHostBuilder app)
    {
        app.Host.AddRabbitMQClient("bus");
        app.Services.AddDirectPublisher();
        app.Services.AddConsumer<SynthesisRequested, SynthesisRequestedMessageHandler>();
    }
}
