using Research.Jobs.Application.Observability;
using Research.Jobs.Domain;
using Research.Jobs.Worker.Features;
using Research.ServiceDefaults;
using Research.Shared.Core.Hosting;
using Research.Shared.Bus.Mediation;
using Research.Shared.Bus.Messaging;
using Research.Shared.Bus.Messaging.Observability;
using Research.Shared.Bus.Mediation.Observability;
using Research.Shared.Bus;

namespace Research.Jobs.Worker.CompositionRoot;

/// <summary>
/// The composition root of the Jobs Worker host: RabbitMQ reply consumers, transactional outbox publisher and timer
/// loops (deadlines, stuck-job recovery). HTTP serves health endpoints only.
/// </summary>
public sealed class JobsWorkerCompositionRoot
{
    private readonly WebApplicationBuilder _builder;

    public JobsWorkerCompositionRoot(WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _builder = builder;
        Settings = new JobsWorkerSettings(builder.Services, builder.Configuration);
    }

    internal JobsWorkerSettings Settings { get; }

    /// <summary>Every feature, in registration order, grouped by concern.</summary>
    internal IReadOnlyList<IHostFeature> Features { get; } =
    [
        // Platform
        new ActorsFeature(),
        new BusFeature(),
        new ErrorsFeature(MessagingErrors.All, JobsErrors.All),

        // Infrastructure
        new PostgresFeature(),
        new MessagingFeature(),

        // Jobs worker handlers and timers
        new JobsModuleFeature(),
        new WorkersFeature(),
    ];

    /// <summary>Registers Aspire service defaults, then each feature in order.</summary>
    public void ComposeServices()
    {
        _builder.AddServiceDefaults(
            activitySources: [MediationTracing.SourceName, MessagingTracing.SourceName, JobsTracing.SourceName],
            meters: [MediationMetrics.MeterName, MessagingMetrics.MeterName, JobsMetrics.MeterName]);
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
