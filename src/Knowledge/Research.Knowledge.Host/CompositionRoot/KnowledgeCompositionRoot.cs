using Research.Hosting.Composition;
using Research.Hosting.Features;
using Research.Knowledge.Api;
using Research.Knowledge.Host.Features;
using Research.Messaging;
using Research.Messaging.Tracing;
using Research.Problems;

namespace Research.Knowledge.Host.CompositionRoot;

/// <summary>
/// The composition root of the Knowledge service: registrations (<see cref="ComposeServices"/>), the middleware order
/// (<see cref="ComposePipeline"/>) and the endpoints (<see cref="ComposeEndpoints"/>).
/// </summary>
public sealed partial class KnowledgeCompositionRoot
{
    private readonly WebApplicationBuilder _builder;

    public KnowledgeCompositionRoot(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _builder = builder;
        Settings = new KnowledgeSettings(builder.Services, builder.Configuration);
    }

    internal KnowledgeSettings Settings { get; }

    /// <summary>Every feature, in registration order, grouped by concern.</summary>
    internal IReadOnlyList<IResearchFeature> Features { get; } =
    [
        // Observability
        new LoggingFeature(),
        new TelemetryFeature(
            sources: [MessagingTracing.SourceName],
            meters: [MessagingMetrics.MeterName, KnowledgeMetrics.MeterName]),
        new HealthChecksFeature(),

        // Platform
        new ActorsFeature(),
        new ErrorsFeature(new ProblemErrorCatalog(), new MessagingErrorCatalog(), new KnowledgeErrorCatalog()),

        // Infrastructure
        new PostgresFeature(),
        new BlobStorageFeature(),
        new EmbeddingsFeature(),
        new MessagingFeature(),

        // Knowledge module
        new KnowledgeModuleFeature(),

        // HTTP API
        new ApiFeature(),
    ];

    public void ComposeServices()
    {
        _builder.Services.AddSingleton(TimeProvider.System);

        var app = new ResearchBuilder(_builder);
        foreach (var feature in Features)
        {
            feature.Register(app);
        }
    }

    /// <summary>Maps the passage search API, the OpenAPI document and the health endpoints.</summary>
    public void ComposeEndpoints(WebApplication app)
    {
        KnowledgeApi.Map(app);
        HealthEndpoints.Map(app);
    }
}
