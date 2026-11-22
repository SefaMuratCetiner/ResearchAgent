using Research.Knowledge.Api.Endpoints;
using Research.Knowledge.Application;
using Research.Knowledge.Bootstrap.Features;
using Research.ServiceDefaults;
using Research.Shared.Adapters.Composition;
using Research.Shared.Adapters.Mediation;
using Research.Shared.Adapters.Messaging;
using Research.Shared.Adapters.Messaging.Tracing;
using Research.Shared.Messaging;
using Research.Shared.Web.Endpoints;
using Research.Shared.Web.Problems;

namespace Research.Knowledge.Bootstrap.CompositionRoot;

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
        // Platform
        new ActorsFeature(),
        new MediationFeature(),
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
        _builder.AddServiceDefaults(
            activitySources: [MediationTracing.SourceName, MessagingTracing.SourceName],
            meters: [MediationMetrics.MeterName, MessagingMetrics.MeterName, KnowledgeMetrics.MeterName],
            configureHttpClientDefaults: false);
        _builder.Services.AddSingleton(TimeProvider.System);

        var app = new ResearchBuilder(_builder);
        foreach (var feature in Features)
        {
            feature.Register(app);
        }
    }

    /// <summary>Maps the passage search API via <see cref="IEndpointGroup"/> discovery, OpenAPI and health endpoints.</summary>
    public void ComposeEndpoints(WebApplication app)
    {
        app.MapEndpoints<KnowledgeEndpoints>();
        if (!app.Environment.IsProduction())
        {
            app.MapOpenApi();
        }

        app.MapDefaultEndpoints();
    }
}
