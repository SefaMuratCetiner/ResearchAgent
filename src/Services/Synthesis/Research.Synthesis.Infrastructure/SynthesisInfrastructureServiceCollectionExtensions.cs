using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Research.Shared.Adapters.Http;
using Research.Synthesis.Application;
using Research.Synthesis.Application.Common.Interfaces;
using Research.Synthesis.Infrastructure.Http;

namespace Research.Synthesis.Infrastructure;

/// <summary>DI registrations for the Synthesis Infrastructure layer.</summary>
public static class SynthesisInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the typed <see cref="HttpClient"/> for <see cref="IKnowledgeRetriever"/> (<see cref="HttpKnowledgeRetriever"/>),
    /// reading <see cref="SynthesisOptions.KnowledgeBaseAddress"/> on each client creation so reloaded addresses apply
    /// without a restart and forwarding <c>X-Correlation-ID</c> via <see cref="CorrelationIdDelegatingHandler"/>.
    /// </summary>
    public static IServiceCollection AddSynthesisInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddTransient<CorrelationIdDelegatingHandler>();
        services.AddHttpClient<IKnowledgeRetriever, HttpKnowledgeRetriever>(static (provider, client) =>
            client.BaseAddress = provider.GetRequiredService<IOptionsMonitor<SynthesisOptions>>().CurrentValue.KnowledgeBaseAddress)
            .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

        return services;
    }
}
