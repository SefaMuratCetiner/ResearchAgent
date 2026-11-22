using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Research.Fetching.Application;
using Research.Fetching.Application.Common.Interfaces;
using Research.Fetching.Infrastructure.Download;
using Research.Fetching.Infrastructure.Extraction;
using Research.Fetching.Infrastructure.Persistence;

namespace Research.Fetching.Infrastructure;

/// <summary>DI registrations for the Fetching Infrastructure layer.</summary>
public static class FetchingInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SSRF-guarded HTTP downloader client, robots policy, per-domain throttle, HTML extractor, and
    /// Postgres repository.
    /// </summary>
    public static IServiceCollection AddFetchingInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IRobotsPolicy, RobotsPolicy>();
        services.TryAddSingleton<IDomainThrottle, DomainThrottle>();
        services.TryAddSingleton<IPageDownloader, PageDownloader>();
        services.TryAddSingleton<IContentExtractor, ContentExtractor>();
        services.TryAddScoped<IFetchedDocumentRepository, FetchedDocumentRepository>();

        // Every connection goes through SsrfGuard (first request, each redirect, robots.txt). No resilience handler:
        // transient failures are retried by the message pipeline with its delays. Timeouts are per call.
        services.AddHttpClient(PageDownloader.HttpClientName, static client => client.Timeout = Timeout.InfiniteTimeSpan)
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

        return services;
    }
}
