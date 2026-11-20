using Research.Shared.Core.Messaging;
using Research.Fetching.Bootstrap.Features;
using Research.ServiceDefaults.Composition;
using Research.ServiceDefaults.Features;
using Research.Shared.Adapters.Messaging;
using Research.Shared.Adapters.Messaging.Tracing;

namespace Research.Fetching.Bootstrap.CompositionRoot;

/// <summary>
/// The composition root of the Fetching service. It has no API: the HTTP surface is health only. It leaves out
/// <see cref="HttpClientsFeature"/>: the only outbound client goes to arbitrary sites, needs no service discovery and
/// is retried at the message level, so <see cref="FetchingModuleFeature"/> configures its own handler.
/// </summary>
public sealed class FetchingCompositionRoot
{
    private readonly WebApplicationBuilder _builder;

    public FetchingCompositionRoot(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _builder = builder;
        Settings = new FetchingSettings(builder.Services, builder.Configuration);
    }

    internal FetchingSettings Settings { get; }

    /// <summary>Every feature, in registration order, grouped by concern.</summary>
    internal IReadOnlyList<IResearchFeature> Features { get; } =
    [
        // Observability
        new LoggingFeature(),
        new TelemetryFeature(
            sources: [MessagingTracing.SourceName],
            meters: [MessagingMetrics.MeterName, FetchingMetrics.MeterName]),
        new HealthChecksFeature(),

        // Platform
        new ActorsFeature(),
        new ErrorsFeature(new MessagingErrorCatalog(), new FetchingErrorCatalog()),

        // Infrastructure
        new PostgresFeature(),
        new BlobStorageFeature(),
        new MessagingFeature(),

        // Fetching module
        new FetchingModuleFeature(),
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

    /// <summary>Health endpoints only.</summary>
    public void ComposeEndpoints(WebApplication app) => HealthEndpoints.Map(app);
}
