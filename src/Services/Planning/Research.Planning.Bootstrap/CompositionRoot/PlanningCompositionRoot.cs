using Research.Planning.Application;
using Research.Planning.Bootstrap.Features;
using Research.ServiceDefaults;
using Research.Shared.Adapters.Composition;
using Research.Shared.Adapters.Llm;
using Research.Shared.Adapters.Mediation;
using Research.Shared.Adapters.Messaging;
using Research.Shared.Adapters.Messaging.Tracing;
using Research.Shared.Ports.Llm;
using Research.Shared.Messaging;

namespace Research.Planning.Bootstrap.CompositionRoot;

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
            // Platform
            new ActorsFeature(),
            new MediationFeature(),
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
        _builder.AddServiceDefaults(
            activitySources: [MediationTracing.SourceName, MessagingTracing.SourceName, LlmTracing.SourceName],
            meters: [MediationMetrics.MeterName, MessagingMetrics.MeterName, LlmMetrics.MeterName]);
        _builder.Services.AddSingleton(TimeProvider.System);

        var app = new ResearchBuilder(_builder);
        foreach (var feature in Features)
        {
            feature.Register(app);
        }
    }

    /// <summary>Health endpoints only.</summary>
    public void ComposeEndpoints(WebApplication app) => app.MapDefaultEndpoints();

    /// <summary>The LLM provider selected by <c>Llm:Provider</c>.</summary>
    private static IResearchFeature LlmFeature(LlmProvider provider) =>
        provider == LlmProvider.OpenAI ? new OpenAIFeature() : new AnthropicFeature();
}
