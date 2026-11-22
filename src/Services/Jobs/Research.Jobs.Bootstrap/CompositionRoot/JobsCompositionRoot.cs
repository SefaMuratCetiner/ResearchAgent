using Research.Jobs.Api;
using Research.Jobs.Application.Observability;
using Research.Jobs.Bootstrap.Features;
using Research.Jobs.Domain;
using Research.ServiceDefaults;
using Research.Shared.Core.Hosting;
using Research.Shared.Bus.Mediation;
using Research.Shared.Bus.Messaging;
using Research.Shared.Web.Endpoints;
using Research.Shared.Web.Problems;
using Research.Shared.Bus.Mediation.Observability;
using Research.Shared.Bus;

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
    internal IReadOnlyList<IHostFeature> Features { get; } =
    [
        // Platform
        new ActorsFeature(),
        new BusFeature(),
        new ErrorsFeature(ApiErrors.All, MessagingErrors.All, JobsErrors.All),

        // Infrastructure
        new PostgresFeature(),

        // Jobs module (API handlers only)
        new JobsModuleFeature(),

        // HTTP API
        new ApiFeature(),
    ];

    /// <summary>Registers Aspire service defaults, then each feature in order.</summary>
    public void ComposeServices()
    {
        _builder.AddServiceDefaults(
            activitySources: [MediationTracing.SourceName, JobsTracing.SourceName],
            meters: [MediationMetrics.MeterName, JobsMetrics.MeterName]);
        _builder.Services.AddSingleton(TimeProvider.System);

        var app = new ServiceHostBuilder(_builder);
        foreach (var feature in Features)
        {
            feature.Register(app);
        }
    }

    /// <summary>Maps the HTTP surface via <see cref="IEndpointGroup"/> discovery, OpenAPI and health endpoints.</summary>
    public void ComposeEndpoints(WebApplication app)
    {
        app.MapEndpoints<JobsEndpoints>();
        if (!app.Environment.IsProduction())
        {
            app.MapOpenApi();
        }

        app.MapDefaultEndpoints();
    }
}
