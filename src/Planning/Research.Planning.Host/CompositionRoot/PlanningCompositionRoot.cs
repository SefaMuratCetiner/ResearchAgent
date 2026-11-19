using Research.Hosting.Composition;
using Research.Hosting.Features;
using Research.Messaging;
using Research.Messaging.Tracing;
using Research.Planning.Host.Features;
using Research.SharedKernel.Llm;

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
        Features =
        [
            // Observability
            new LoggingFeature(),
            new TelemetryFeature(
                sources: [MessagingTracing.SourceName, LlmTracing.SourceName],
                meters: [MessagingMetrics.MeterName, LlmMetrics.MeterName]),
            new HealthChecksFeature(),

            // Platform
            new ActorsFeature(),
            new HttpClientsFeature(),
            new ErrorsFeature(new MessagingErrorCatalog(), new PlanningErrorCatalog()),

            // Infrastructure
            new ValkeyFeature(),
            LlmFeature(Settings.Llm.Provider),
            new MessagingFeature(),

            // Planning module
            new PlanningModuleFeature(),
        ];
    }

    internal PlanningSettings Settings { get; }

    /// <summary>Every feature, in registration order, grouped by concern.</summary>
    internal IReadOnlyList<IResearchFeature> Features { get; }

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

    /// <summary>The LLM provider selected by <c>Llm:Provider</c>.</summary>
    private static IResearchFeature LlmFeature(LlmProvider provider) =>
        provider == LlmProvider.OpenAI ? new OpenAIFeature() : new AnthropicFeature();
}
