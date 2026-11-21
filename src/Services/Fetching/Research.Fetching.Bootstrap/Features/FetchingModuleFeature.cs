using System.Net;
using Microsoft.Extensions.Options;
using Research.Fetching.Application;
using Research.Fetching.Application.Common.Interfaces;
using Research.Fetching.Application.FetchPage;
using Research.Fetching.Infrastructure.Download;
using Research.Fetching.Infrastructure.Extraction;
using Research.Fetching.Infrastructure.Persistence;
using Research.Shared.Adapters.Composition;

namespace Research.Fetching.Bootstrap.Features;

/// <summary>
/// The Fetching module: the SSRF-guarded download client, robots policy, per-domain throttle, extractor, repository and handler.
/// </summary>
internal sealed class FetchingModuleFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Services.AddSingleton<FetchingMetrics>();
        app.Services.AddSingleton<IRobotsPolicy, RobotsPolicy>();
        app.Services.AddSingleton<IDomainThrottle, DomainThrottle>();
        app.Services.AddSingleton<IPageDownloader, PageDownloader>();
        app.Services.AddSingleton<IContentExtractor, ContentExtractor>();
        app.Services.AddScoped<IFetchedDocumentRepository, FetchedDocumentRepository>();
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
