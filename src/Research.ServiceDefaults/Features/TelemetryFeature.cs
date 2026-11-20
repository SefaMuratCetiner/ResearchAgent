using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Research.ServiceDefaults.Composition;

namespace Research.ServiceDefaults.Features;

/// <summary>
/// Traces and metrics over OTLP. The Host passes the activity sources and meters of its libraries explicitly;
/// Aspire client integrations add their own (Npgsql, RabbitMQ, StackExchange.Redis, Azure). The service name and
/// OTLP endpoint come from the standard <c>OTEL_*</c> variables, which Aspire sets.
/// </summary>
/// <param name="sources">Activity source names to sample (<c>Research.Messaging</c>, <c>Research.Jobs</c>).</param>
/// <param name="meters">Meter names to export.</param>
public sealed class TelemetryFeature(IReadOnlyList<string> sources, IReadOnlyList<string> meters) : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var telemetry = app.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource([.. sources])
                .AddAspNetCoreInstrumentation(static options => options.Filter = static context => !IsHealthRequest(context))
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddMeter([.. meters])
                .AddMeter("System.Runtime")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        if (!string.IsNullOrWhiteSpace(app.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.UseOtlpExporter();
        }
    }

    private static bool IsHealthRequest(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
}
