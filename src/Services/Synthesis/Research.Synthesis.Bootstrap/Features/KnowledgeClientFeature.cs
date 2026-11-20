using Microsoft.Extensions.Options;
using Research.ServiceDefaults.Composition;
using Research.Synthesis.Infrastructure.Http;
using Research.Synthesis.Retrieval;

namespace Research.Synthesis.Bootstrap.Features;

/// <summary>
/// The Knowledge API client: a typed <see cref="HttpClient"/> whose base address is resolved by Aspire service discovery
/// (<c>https+http://knowledge</c>), with the standard resilience handler from <c>HttpClientsFeature</c>. The address is
/// read each time a client is created (once per message scope), so a reloaded <c>Synthesis:KnowledgeBaseAddress</c>
/// applies without a restart.
/// </summary>
internal sealed class KnowledgeClientFeature : IResearchFeature
{
    public void Register(ResearchBuilder app) =>
        app.Services.AddHttpClient<IKnowledgeRetriever, HttpKnowledgeRetriever>(static (provider, client) =>
            client.BaseAddress = provider.GetRequiredService<IOptionsMonitor<SynthesisOptions>>().CurrentValue.KnowledgeBaseAddress);
}
