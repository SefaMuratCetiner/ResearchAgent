using Research.Planning.Application;
using Research.Planning.Bootstrap.Features;
using Research.ServiceDefaults;
using Research.Shared.Core.Hosting;
using Research.Shared.Core.Llm;
using Research.Shared.Bus.Mediation;
using Research.Shared.Bus.Messaging;
using Research.Shared.Bus.Messaging.Observability;
using Research.Shared.Core.Llm.Observability;
using Research.Shared.Bus.Mediation.Observability;
using Research.Shared.Bus;

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
            new BusFeature(),
            new ErrorsFeature(MessagingErrors.All, PlanningErrors.All),

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
    internal IReadOnlyList<IHostFeature> Features { get; }

    public void ComposeServices()
    {
        _builder.AddServiceDefaults(
            activitySources: [MediationTracing.SourceName, MessagingTracing.SourceName, LlmTracing.SourceName],
            meters: [MediationMetrics.MeterName, MessagingMetrics.MeterName, LlmMetrics.MeterName]);
        _builder.Services.AddSingleton(TimeProvider.System);

        var app = new ServiceHostBuilder(_builder);
        foreach (var feature in Features)
        {
            feature.Register(app);
        }
    }

    /// <summary>Health endpoints only.</summary>
    public void ComposeEndpoints(WebApplication app) => app.MapDefaultEndpoints();

    /// <summary>The LLM provider selected by <c>Llm:Provider</c>.</summary>
    private static IHostFeature LlmFeature(LlmProvider provider) =>
        provider == LlmProvider.OpenAI ? new OpenAIFeature() : new AnthropicFeature();
}
