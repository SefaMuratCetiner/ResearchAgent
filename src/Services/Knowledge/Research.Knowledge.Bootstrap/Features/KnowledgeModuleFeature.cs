using Research.Knowledge.Application;
using Research.Knowledge.Infrastructure;
using Research.Shared.Adapters.Composition;

namespace Research.Knowledge.Bootstrap.Features;

/// <summary>The Knowledge module: repository, metrics and one handler per slice. The message handler is in <see cref="MessagingFeature"/>.</summary>
internal sealed class KnowledgeModuleFeature : IHostFeature
{
    public void Register(ServiceHostBuilder app)
    {
        app.Services.AddKnowledgeApplication();
        app.Services.AddKnowledgeInfrastructure();
    }
}
