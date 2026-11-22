using Research.Fetching.Application.Observability;
using Research.Fetching.Bootstrap.Features;
using Research.Fetching.Domain;
using Research.ServiceDefaults;
using Research.Shared.Core.Hosting;
using Research.Shared.Bus.Mediation;
using Research.Shared.Bus.Messaging;
using Research.Shared.Bus.Messaging.Observability;
using Research.Shared.Bus.Mediation.Observability;
using Research.Shared.Bus;

namespace Research.Fetching.Bootstrap.CompositionRoot;

/// <summary>
/// The composition root of the Fetching service. It has no API: the HTTP surface is health only. It disables default
/// HTTP client resilience/service discovery: the only outbound client goes to arbitrary sites and is retried at the
/// message level, so <see cref="FetchingModuleFeature"/> configures its own handler.
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
    internal IReadOnlyList<IHostFeature> Features { get; } =
    [
        // Platform
        new ActorsFeature(),
        new BusFeature(),
        new ErrorsFeature(MessagingErrors.All, FetchingErrors.All),

        // Infrastructure
        new PostgresFeature(),
        new BlobStorageFeature(),
        new MessagingFeature(),

        // Fetching module
        new FetchingModuleFeature(),
    ];

    public void ComposeServices()
    {
        _builder.AddServiceDefaults(
            activitySources: [MediationTracing.SourceName, MessagingTracing.SourceName],
            meters: [MediationMetrics.MeterName, MessagingMetrics.MeterName, FetchingMetrics.MeterName],
            configureHttpClientDefaults: false);
        _builder.Services.AddSingleton(TimeProvider.System);

        var app = new ServiceHostBuilder(_builder);
        foreach (var feature in Features)
        {
            feature.Register(app);
        }
    }

    /// <summary>Health endpoints only.</summary>
    public void ComposeEndpoints(WebApplication app) => app.MapDefaultEndpoints();
}
