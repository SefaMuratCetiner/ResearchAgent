using System.Globalization;
using System.Reflection;
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
using Serilog.Extensions.Hosting;
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

    /// <summary>
    /// Creates the pre-host bootstrap logger using the same compact JSON format and service/environment/instance
    /// enrichment as the configured host logger, so startup and fatal failures match the regular stdout log schema.
    /// <see cref="AddServiceDefaults{TBuilder}"/> replaces it once the host is built.
    /// </summary>
    public static ReloadableLogger CreateBootstrapLogger(string? serviceName = null)
    {
        var resolvedService = FirstNonEmpty(
            Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME"),
            serviceName,
            Assembly.GetEntryAssembly()?.GetName().Name) ?? "unknown";
        var environmentName = FirstNonEmpty(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")) ?? Environments.Production;
        var instanceId = ResolveInstanceId(Environment.GetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES"));

        return new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("ServiceName", resolvedService)
            .Enrich.WithProperty("Environment", environmentName)
            .Enrich.WithProperty("ServiceInstanceId", instanceId)
            .WriteTo.Console(new RenderedCompactJsonFormatter())
            .CreateBootstrapLogger();
    }

    private static void ConfigureLogging<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Clear the default MEL providers (Console, Debug, EventSource, EventLog) before adding OpenTelemetry so
        // Serilog's writeToProviders: true forwards only to OpenTelemetry and does not duplicate stdout output.
        builder.Logging.ClearProviders();
        builder.Logging.AddOpenTelemetry(static options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
            options.ParseStateValues = true;
        });

        var elasticsearch = builder.Configuration.GetConnectionString("elasticsearch");
        // Aspire names each replica's service in OTEL_SERVICE_NAME and replica id in OTEL_RESOURCE_ATTRIBUTES.
        var serviceName = FirstNonEmpty(builder.Configuration["OTEL_SERVICE_NAME"], builder.Environment.ApplicationName) ?? "unknown";
        var environmentName = builder.Environment.EnvironmentName;
        var instanceId = ResolveInstanceId(builder.Configuration["OTEL_RESOURCE_ATTRIBUTES"]);

        builder.Services.AddSerilog(
            (services, logger) =>
            {
                logger
                    .ReadFrom.Configuration(builder.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("ServiceName", serviceName)
                    .Enrich.WithProperty("Environment", environmentName)
                    .Enrich.WithProperty("ServiceInstanceId", instanceId)
                    .WriteTo.Console(new RenderedCompactJsonFormatter());

                if (!string.IsNullOrEmpty(elasticsearch))
                {
                    WriteToElasticsearch(logger, new Uri(elasticsearch), serviceName, environmentName, instanceId);
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
    private static void WriteToElasticsearch(
        LoggerConfiguration logger,
        Uri connection,
        string serviceName,
        string environmentName,
        string instanceId)
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
                        document.Service.Environment = environmentName;
                        document.Service.NodeName = instanceId;
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

    internal static string ResolveInstanceId(string? resourceAttributes)
    {
        if (!string.IsNullOrWhiteSpace(resourceAttributes))
        {
            foreach (var entry in resourceAttributes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                const string Key = "service.instance.id=";
                if (entry.StartsWith(Key, StringComparison.Ordinal))
                {
                    var value = Uri.UnescapeDataString(entry[Key.Length..]).Trim();
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }
            }
        }

        return string.Create(CultureInfo.InvariantCulture, $"{Environment.MachineName}:{Environment.ProcessId}");
    }

    private static string? FirstNonEmpty(params string?[] candidates) =>
        candidates.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
}
