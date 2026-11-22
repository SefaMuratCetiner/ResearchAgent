using Research.Contracts.Knowledge;
using Research.Contracts.Planning;
using Research.Contracts.Search;
using Research.Contracts.Steps;
using Research.Contracts.Synthesis;
using Research.Jobs.Application.AdvanceOnDocumentIndexed;
using Research.Jobs.Application.AdvanceOnPlanReady;
using Research.Jobs.Application.AdvanceOnResultsFound;
using Research.Jobs.Application.CompleteOnReportReady;
using Research.Jobs.Application.HandleStepFailure;
using Research.Providers.RabbitMq;
using Research.Shared.Adapters.Composition;

namespace Research.Jobs.Worker.Features;

/// <summary>
/// RabbitMQ (Aspire connection <c>bus</c>): the confirmed publisher behind the outbox dispatcher, and one listener per
/// reply Jobs consumes. Everything Jobs publishes goes through the outbox.
/// </summary>
internal sealed class MessagingFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
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
