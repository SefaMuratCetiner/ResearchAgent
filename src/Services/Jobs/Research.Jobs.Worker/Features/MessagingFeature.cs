using Research.Shared.Contracts.Knowledge;
using Research.Shared.Contracts.Planning;
using Research.Shared.Contracts.Search;
using Research.Shared.Contracts.Steps;
using Research.Shared.Contracts.Synthesis;
using Research.Jobs.Application.AdvanceOnDocumentIndexed;
using Research.Jobs.Application.AdvanceOnPlanReady;
using Research.Jobs.Application.AdvanceOnResultsFound;
using Research.Jobs.Application.CompleteOnReportReady;
using Research.Jobs.Application.HandleStepFailure;
using Research.Providers.RabbitMq;
using Research.Shared.Core.Hosting;

namespace Research.Jobs.Worker.Features;

/// <summary>
/// RabbitMQ (Aspire connection <c>bus</c>): the confirmed publisher behind the outbox dispatcher, and one listener per
/// reply Jobs consumes. Everything Jobs publishes goes through the outbox.
/// </summary>
internal sealed class MessagingFeature : IHostFeature
{
    public void Register(ServiceHostBuilder app)
    {
        app.Host.AddRabbitMQClient("bus");
        app.Services.AddOutboxPublisher();

        app.Services.AddConsumer<PlanReady, PlanReadyMessageHandler>();
        app.Services.AddConsumer<ResultsFound, ResultsFoundMessageHandler>();
        app.Services.AddConsumer<DocumentIndexed, DocumentIndexedMessageHandler>();
        app.Services.AddConsumer<ReportReady, ReportReadyMessageHandler>();
        app.Services.AddConsumer<StepFailed, StepFailedMessageHandler>();
    }
}
