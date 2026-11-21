using Research.ServiceDefaults.Composition;
using Research.Knowledge.Application.Common.Interfaces;
using Research.Knowledge.Domain;
using Research.Knowledge.IndexDocument;
using Research.Knowledge.Infrastructure.Persistence;
using Research.Knowledge.SearchPassages;

namespace Research.Knowledge.Bootstrap.Features;

/// <summary>The Knowledge module: repository, metrics and one handler per slice. The message handler is in <see cref="MessagingFeature"/>.</summary>
internal sealed class KnowledgeModuleFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Services.AddSingleton<KnowledgeMetrics>();
        app.Services.AddScoped<IKnowledgeRepository, KnowledgeRepository>();
        app.Services.AddScoped<PrepareDocumentHandler>();
        app.Services.AddScoped<SearchPassagesHandler>();
    }
}
