using System.Net;
using Microsoft.Extensions.Options;
using Research.Fetching.Domain;
using Research.Fetching.Download;
using Research.Fetching.FetchPage;
using Research.Fetching.Postgres;
using Research.Hosting.Composition;

namespace Research.Fetching.Host.Features;

/// <summary>
/// The Fetching module: the SSRF-guarded download client, robots policy, per-domain throttle, repository and handler.
/// </summary>
internal sealed class FetchingModuleFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Services.AddSingleton<FetchingMetrics>();
        app.Services.AddSingleton<RobotsPolicy>();
        app.Services.AddSingleton<DomainThrottle>();
        app.Services.AddSingleton<PageDownloader>();
        app.Services.AddSingleton<IFetchedDocumentRepository, FetchedDocumentRepository>();
        app.Services.AddScoped<FetchPageHandler>();

        // Every connection goes through SsrfGuard (first request, each redirect, robots.txt). No resilience handler:
        // transient failures are retried by the message pipeline with its delays. Timeouts are per call.
        app.Services.AddHttpClient(PageDownloader.HttpClientName, static client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(static provider =>
            {
                // Read when the factory builds a handler (every handler lifetime, 2 minutes by default), so a
                // changed MaxRedirects applies to new connections without a restart.
                var options = provider.GetRequiredService<IOptionsMonitor<FetchingOptions>>().CurrentValue;
                return new SocketsHttpHandler
                {
                    ConnectCallback = SsrfGuard.ConnectAsync,
                    AllowAutoRedirect = options.MaxRedirects > 0,
                    MaxAutomaticRedirections = Math.Max(1, options.MaxRedirects),
                    AutomaticDecompression = DecompressionMethods.All,
                    UseCookies = false,
                    UseProxy = false,
                    ConnectTimeout = TimeSpan.FromSeconds(10),
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    MaxResponseHeadersLength = 64,
                };
            });
    }
}
