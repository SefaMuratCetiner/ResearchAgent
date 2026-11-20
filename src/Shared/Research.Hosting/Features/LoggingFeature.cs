using System.Threading.Channels;
using Elastic.Channels;
using Elastic.CommonSchema;
using Elastic.CommonSchema.Serilog;
using Elastic.Ingest.Elasticsearch;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Research.Hosting.Composition;
using Serilog;
using Serilog.Formatting.Compact;

namespace Research.Hosting.Features;

/// <summary>
/// Logging: Serilog writes compact JSON to stdout (levels and per-category overrides from the <c>Serilog</c> section,
/// reloadable), and forwards every event to the OpenTelemetry logging provider, which exports over OTLP with the
/// trace and span ids of the current activity. When the <c>elasticsearch</c> connection string is set, every event
/// also goes to the Elasticsearch data stream <c>logs-dotnet-default</c> in ECS format, sent in the background in
/// batches; when Elasticsearch is unreachable the oldest buffered events are dropped, and logging never blocks.
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

        var elasticsearch = app.Configuration.GetConnectionString("elasticsearch");
        // Aspire names each replica's service in OTEL_SERVICE_NAME; it becomes service.name in Elasticsearch.
        var serviceName = app.Configuration["OTEL_SERVICE_NAME"] ?? app.Environment.ApplicationName;

        app.Services.AddSerilog(
            (services, logger) =>
            {
                logger
                    .ReadFrom.Configuration(app.Configuration)
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
