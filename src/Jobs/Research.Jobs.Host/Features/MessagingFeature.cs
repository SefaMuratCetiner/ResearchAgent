using Research.Contracts.Knowledge;
using Research.Contracts.Planning;
using Research.Contracts.Search;
using Research.Contracts.Steps;
using Research.Contracts.Synthesis;
using Research.Hosting.Composition;
using Research.Jobs.AdvanceOnDocumentIndexed;
using Research.Jobs.AdvanceOnPlanReady;
using Research.Jobs.AdvanceOnResultsFound;
using Research.Jobs.CompleteOnReportReady;
using Research.Jobs.HandleStepFailure;
using Research.Messaging.Outbox;
using Research.Providers.RabbitMq;

namespace Research.Jobs.Host.Features;

/// <summary>
/// RabbitMQ (Aspire connection <c>bus</c>): the confirmed publisher behind the outbox dispatcher, and one listener per
/// reply Jobs consumes. Everything Jobs publishes goes through the outbox.
/// </summary>
internal sealed class MessagingFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Host.AddRabbitMQClient("bus");

        RabbitMqServiceCollectionExtensions.AddPublisher(app.Services);
        app.Services.AddHostedService<OutboxBackgroundService>();

        RabbitMqServiceCollectionExtensions.AddConsumer<PlanReady, PlanReadyMessageHandler>(app.Services);
        RabbitMqServiceCollectionExtensions.AddConsumer<ResultsFound, ResultsFoundMessageHandler>(app.Services);
        RabbitMqServiceCollectionExtensions.AddConsumer<DocumentIndexed, DocumentIndexedMessageHandler>(app.Services);
        RabbitMqServiceCollectionExtensions.AddConsumer<ReportReady, ReportReadyMessageHandler>(app.Services);
        RabbitMqServiceCollectionExtensions.AddConsumer<StepFailed, StepFailedMessageHandler>(app.Services);
    }
}
