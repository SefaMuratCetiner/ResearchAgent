using System.Threading.Channels;
using Elastic.Channels;
using Elastic.CommonSchema;
using Elastic.CommonSchema.Serilog;
using Elastic.Ingest.Elasticsearch;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

namespace Research.ServiceDefaults;

/// <summary>
/// Standard .NET Aspire service defaults: structured logging (Serilog + OTel + optional Elasticsearch),
/// OpenTelemetry tracing and metrics over OTLP, health checks and endpoints, service discovery and default HTTP resilience.
/// </summary>
public static class ServiceDefaultsExtensions
{
    public const string HealthLivePath = "/health/live";
    public const string HealthReadyPath = "/health/ready";

    /// <summary>
    /// Registers standard Aspire observability, health checks, service discovery and default HTTP client resilience.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="activitySources">Additional OpenTelemetry activity source names to sample.</param>
    /// <param name="meters">Additional OpenTelemetry meter names to export.</param>
    /// <param name="configureHttpClientDefaults">
    /// Whether to apply Aspire service discovery and the standard resilience handler to all <see cref="HttpClient"/>s.
    /// Set to <c>false</c> for hosts whose outbound HTTP clients require custom transport policies.
    /// </param>
    public static TBuilder AddServiceDefaults<TBuilder>(
        this TBuilder builder,
        IReadOnlyList<string>? activitySources = null,
        IReadOnlyList<string>? meters = null,
        bool configureHttpClientDefaults = true)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureLogging();
        builder.ConfigureOpenTelemetry(activitySources ?? [], meters ?? []);
        builder.AddDefaultHealthChecks();

        if (configureHttpClientDefaults)
        {
            builder.Services.AddServiceDiscovery();
            builder.Services.ConfigureHttpClientDefaults(static http =>
            {
                http.AddStandardResilienceHandler();
                http.AddServiceDiscovery();
            });
        }

        return builder;
    }

    /// <summary>
    /// Maps <c>/health/live</c> (liveness, runs no checks) and <c>/health/ready</c> (readiness, runs all registered checks).
    /// Degraded checks report 200 so a non-critical outage does not take a replica out of rotation.
    /// </summary>
    public static IEndpointRouteBuilder MapDefaultEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHealthChecks(HealthLivePath, new HealthCheckOptions { Predicate = static _ => false });
        endpoints.MapHealthChecks(HealthReadyPath);
        return endpoints;
    }

    private static void ConfigureLogging<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(static options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
            options.ParseStateValues = true;
        });

        var elasticsearch = builder.Configuration.GetConnectionString("elasticsearch");
        // Aspire names each replica's service in OTEL_SERVICE_NAME; it becomes service.name in Elasticsearch.
        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? builder.Environment.ApplicationName;

        builder.Services.AddSerilog(
            (services, logger) =>
            {
                logger
                    .ReadFrom.Configuration(builder.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(new RenderedCompactJsonFormatter());

                if (!string.IsNullOrEmpty(elasticsearch))
                {
                    WriteToElasticsearch(logger, new Uri(elasticsearch), serviceName);
                }
            },
            writeToProviders: true);
    }

    private static void ConfigureOpenTelemetry<TBuilder>(
        this TBuilder builder,
        IReadOnlyList<string> activitySources,
        IReadOnlyList<string> meters)
        where TBuilder : IHostApplicationBuilder
    {
        var telemetry = builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource([.. activitySources])
                .AddAspNetCoreInstrumentation(static options => options.Filter = static context => !IsHealthRequest(context))
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddMeter([.. meters])
                .AddMeter("System.Runtime")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.UseOtlpExporter();
        }
    }

    private static void AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder =>
        builder.Services.AddHealthChecks();

    private static bool IsHealthRequest(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);

    /// <summary>Adds the Elasticsearch sink; <paramref name="connection"/> is Aspire's <c>http://user:password@host:port</c>.</summary>
    private static void WriteToElasticsearch(LoggerConfiguration logger, Uri connection, string serviceName)
    {
        var credentials = connection.UserInfo.Split(':', 2);
        var node = new UriBuilder(connection) { UserName = string.Empty, Password = string.Empty }.Uri;

        logger.WriteTo.Elasticsearch(
            [node],
            options =>
            {
                options.BootstrapMethod = BootstrapMethod.Silent;
                options.TextFormatting = new EcsTextFormatterConfiguration<LogEventEcsDocument>
                {
                    MapCustom = (document, _) =>
                    {
                        document.Service ??= new Service();
                        document.Service.Name = serviceName;
                        return document;
                    },
                };
                options.ConfigureChannel = channel => channel.BufferOptions = new BufferOptions
                {
                    BoundedChannelFullMode = BoundedChannelFullMode.DropOldest,
                };
            },
            transport =>
            {
                if (credentials is [var user, var password])
                {
                    transport.Authentication(new BasicAuthentication(Uri.UnescapeDataString(user), Uri.UnescapeDataString(password)));
                }
            });
    }
}
