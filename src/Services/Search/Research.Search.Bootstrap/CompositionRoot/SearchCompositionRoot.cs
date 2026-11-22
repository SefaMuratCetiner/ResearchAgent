using Research.Search.Application.Observability;
using Research.Search.Bootstrap.Features;
using Research.ServiceDefaults;
using Research.Shared.Core.Hosting;
using Research.Shared.Bus.Mediation;
using Research.Shared.Bus.Messaging;
using Research.Shared.Bus.Messaging.Observability;
using Research.Shared.Core.Search;
using Research.Shared.Bus.Mediation.Observability;
using Research.Shared.Bus;

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
    internal IReadOnlyList<IHostFeature> Features { get; } =
    [
        // Platform
        new ActorsFeature(),
        new BusFeature(),
        new ErrorsFeature(MessagingErrors.All, SearchErrors.All),

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
            activitySources: [MediationTracing.SourceName, MessagingTracing.SourceName],
            meters: [MediationMetrics.MeterName, MessagingMetrics.MeterName, SearchMetrics.MeterName]);
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
