using Research.Jobs.Api;
using Research.Jobs.Bootstrap.Features;
using Research.Jobs.Observability;
using Research.ServiceDefaults.Composition;
using Research.ServiceDefaults.Features;
using Research.ServiceDefaults.Problems;
using Research.Shared.Core.Messaging;

namespace Research.Jobs.Bootstrap.CompositionRoot;

/// <summary>
/// The composition root of the Jobs HTTP API host: registrations (<see cref="ComposeServices"/>), the middleware order
/// (<see cref="ComposePipeline"/>) and the endpoints (<see cref="ComposeEndpoints"/>). Background consumers, outbox
/// publishing and timer loops run separately in <c>Research.Jobs.Worker</c>.
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
            sources: [JobsTracing.SourceName],
            meters: [JobsMetrics.MeterName]),
        new HealthChecksFeature(),

        // Platform
        new ActorsFeature(),
        new HttpClientsFeature(),
        new ErrorsFeature(new ProblemErrorCatalog(), new MessagingErrorCatalog(), new JobsErrorCatalog()),

        // Infrastructure
        new PostgresFeature(),

        // Jobs module (API handlers only)
        new JobsModuleFeature(),

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
