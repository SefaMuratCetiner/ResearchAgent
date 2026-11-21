using Research.Search.Application;
using Research.Search.Bootstrap.Features;
using Research.ServiceDefaults;
using Research.Shared.Adapters.Composition;
using Research.Shared.Adapters.Messaging;
using Research.Shared.Adapters.Messaging.Tracing;
using Research.Shared.Core.Messaging;

namespace Research.Search.Bootstrap.CompositionRoot;

/// <summary>The composition root of the Search service. It has no API: the HTTP surface is health only.</summary>
public sealed class SearchCompositionRoot
{
    private readonly WebApplicationBuilder _builder;

    public SearchCompositionRoot(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _builder = builder;
        Settings = new SearchSettings(builder.Services, builder.Configuration);
    }

    internal SearchSettings Settings { get; }

    /// <summary>Every feature, in registration order. The search provider must be registered before its decorators.</summary>
    internal IReadOnlyList<IResearchFeature> Features { get; } =
    [
        // Platform
        new ActorsFeature(),
        new ErrorsFeature(new MessagingErrorCatalog(), new SearchErrorCatalog()),

        // Infrastructure
        new ValkeyFeature(),
        new MessagingFeature(),

        // Search module: provider first, then the decorators that wrap it.
        new AnthropicWebSearchFeature(),
        new SearchModuleFeature(),
    ];

    public void ComposeServices()
    {
        _builder.AddServiceDefaults(
            activitySources: [MessagingTracing.SourceName],
            meters: [MessagingMetrics.MeterName, SearchMetrics.MeterName]);
        _builder.Services.AddSingleton(TimeProvider.System);

        var app = new ResearchBuilder(_builder);
        foreach (var feature in Features)
        {
            feature.Register(app);
        }
    }

    /// <summary>Health endpoints only.</summary>
    public void ComposeEndpoints(WebApplication app) => app.MapDefaultEndpoints();
}
