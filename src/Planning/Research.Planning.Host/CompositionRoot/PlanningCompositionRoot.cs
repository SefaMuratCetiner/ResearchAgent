using Research.Hosting.Composition;
using Research.Hosting.Features;
using Research.Messaging;
using Research.Messaging.Tracing;
using Research.Planning.Host.Features;
using Research.Providers.Anthropic;

namespace Research.Planning.Host.CompositionRoot;

/// <summary>The composition root of the Planning service. It has no API: the HTTP surface is health only.</summary>
public sealed class PlanningCompositionRoot
{
    private readonly WebApplicationBuilder _builder;

    public PlanningCompositionRoot(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _builder = builder;
        Settings = new PlanningSettings(builder.Services, builder.Configuration);
    }

    internal PlanningSettings Settings { get; }

    /// <summary>Every feature, in registration order, grouped by concern.</summary>
    internal IReadOnlyList<IResearchFeature> Features { get; } =
    [
        // Observability
        new LoggingFeature(),
        new TelemetryFeature(
            sources: [MessagingTracing.SourceName, AnthropicLlmClient.ActivitySourceName],
            meters: [MessagingMetrics.MeterName, LlmMetrics.MeterName]),
        new HealthChecksFeature(),

        // Platform
        new ActorsFeature(),
        new HttpClientsFeature(),
        new ErrorsFeature(new MessagingErrorCatalog(), new PlanningErrorCatalog()),

        // Infrastructure
        new ValkeyFeature(),
        new AnthropicFeature(),
        new MessagingFeature(),

        // Planning module
        new PlanningModuleFeature(),
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
