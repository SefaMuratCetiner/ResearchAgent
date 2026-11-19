using Research.Hosting.Composition;
using Research.Hosting.Features;
using Research.Jobs.Api;
using Research.Jobs.Host.Features;
using Research.Jobs.Observability;
using Research.Messaging;
using Research.Messaging.Tracing;
using Research.Problems;

namespace Research.Jobs.Host.CompositionRoot;

/// <summary>
/// The composition root of the Jobs service: every registration (<see cref="ComposeServices"/>), the middleware order
/// (<see cref="ComposePipeline"/>) and the endpoints (<see cref="ComposeEndpoints"/>), readable in one place.
/// </summary>
public sealed partial class JobsCompositionRoot
{
    private readonly WebApplicationBuilder _builder;

    public JobsCompositionRoot(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _builder = builder;
        Settings = new JobsSettings(builder.Services, builder.Configuration);
    }

    internal JobsSettings Settings { get; }

    /// <summary>Every feature, in registration order, grouped by concern.</summary>
    internal IReadOnlyList<IResearchFeature> Features { get; } =
    [
        // Observability
        new LoggingFeature(),
        new TelemetryFeature(
            sources: [MessagingTracing.SourceName, JobsTracing.SourceName],
            meters: [MessagingMetrics.MeterName, JobsMetrics.MeterName]),
        new HealthChecksFeature(),

        // Platform
        new ActorsFeature(),
        new HttpClientsFeature(),
        new ErrorsFeature(new ProblemErrorCatalog(), new MessagingErrorCatalog(), new JobsErrorCatalog()),

        // Infrastructure
        new PostgresFeature(),
        new MessagingFeature(),

        // Jobs module
        new JobsModuleFeature(),
        new WorkersFeature(),

        // HTTP API
        new ApiFeature(),
    ];

    /// <summary>Registers every service: shared infrastructure, then each feature in order.</summary>
    public void ComposeServices()
    {
        _builder.Services.AddSingleton(TimeProvider.System);

        var app = new ResearchBuilder(_builder);
        foreach (var feature in Features)
        {
            feature.Register(app);
        }
    }

    /// <summary>Maps the HTTP surface: the research API, the OpenAPI document and the health endpoints.</summary>
    public void ComposeEndpoints(WebApplication app)
    {
        JobsApi.Map(app);
        HealthEndpoints.Map(app);
    }
}
