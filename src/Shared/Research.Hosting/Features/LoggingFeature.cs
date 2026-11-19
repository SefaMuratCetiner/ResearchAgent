using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Research.Hosting.Composition;
using Serilog;
using Serilog.Formatting.Compact;

namespace Research.Hosting.Features;

/// <summary>
/// Logging: Serilog writes compact JSON to stdout (levels and per-category overrides from the <c>Serilog</c> section,
/// reloadable), and forwards every event to the OpenTelemetry logging provider, which exports over OTLP with the
/// trace and span ids of the current activity.
/// </summary>
public sealed class LoggingFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Logging.AddOpenTelemetry(static options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
            options.ParseStateValues = true;
        });

        app.Services.AddSerilog(
            (services, logger) => logger
                .ReadFrom.Configuration(app.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console(new RenderedCompactJsonFormatter()),
            writeToProviders: true);
    }
}
