using Microsoft.Extensions.DependencyInjection;
using Research.ServiceDefaults.Composition;

namespace Research.ServiceDefaults.Features;

/// <summary>
/// Defaults for every <see cref="System.Net.Http.HttpClient"/> from <see cref="IHttpClientFactory"/>: Aspire service
/// discovery (<c>https+http://knowledge</c>) and the standard resilience handler (rate limiter, total timeout, retry,
/// circuit breaker, attempt timeout). A client that needs different behaviour configures its own handler.
/// </summary>
public sealed class HttpClientsFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Services.AddServiceDiscovery();
        app.Services.ConfigureHttpClientDefaults(static http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });
    }
}
