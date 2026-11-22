using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
    /// without a restart.
    /// </summary>
    public static IServiceCollection AddSynthesisInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient<IKnowledgeRetriever, HttpKnowledgeRetriever>(static (provider, client) =>
            client.BaseAddress = provider.GetRequiredService<IOptionsMonitor<SynthesisOptions>>().CurrentValue.KnowledgeBaseAddress);

        return services;
    }
}
