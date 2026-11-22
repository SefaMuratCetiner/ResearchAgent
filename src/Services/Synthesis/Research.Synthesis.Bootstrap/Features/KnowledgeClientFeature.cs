using Research.Shared.Adapters.Composition;
using Research.Synthesis.Infrastructure;

namespace Research.Synthesis.Bootstrap.Features;

/// <summary>
/// The Knowledge API client: a typed <see cref="HttpClient"/> whose base address is resolved by Aspire service discovery
/// (<c>https+http://knowledge</c>), with the standard resilience handler from <c>AddServiceDefaults</c>. The address is
/// read each time a client is created (once per message scope), so a reloaded <c>Synthesis:KnowledgeBaseAddress</c>
/// applies without a restart.
/// </summary>
internal sealed class KnowledgeClientFeature : IHostFeature
{
    public void Register(ServiceHostBuilder app) => app.Services.AddSynthesisInfrastructure();
}
