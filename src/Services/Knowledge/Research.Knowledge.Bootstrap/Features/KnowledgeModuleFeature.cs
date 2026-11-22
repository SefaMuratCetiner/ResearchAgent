using Research.Knowledge.Application;
using Research.Knowledge.Application.Common.Interfaces;
using Research.Knowledge.Application.IndexDocument;
using Research.Knowledge.Application.SearchPassages;
using Research.Knowledge.Infrastructure.Persistence;
using Research.Shared.Adapters.Composition;
using Research.Shared.Adapters.Mediation;
using Research.SharedKernel.Results;

namespace Research.Knowledge.Bootstrap.Features;

/// <summary>The Knowledge module: repository, metrics and one handler per slice. The message handler is in <see cref="MessagingFeature"/>.</summary>
internal sealed class KnowledgeModuleFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        app.Services.AddSingleton<KnowledgeMetrics>();
        app.Services.AddScoped<IKnowledgeRepository, KnowledgeRepository>();
        app.Services.AddCommandHandler<PrepareDocumentCommand, Result<PreparedDocument>, PrepareDocumentHandler>();
        app.Services.AddQueryHandler<SearchPassagesQuery, Result<IReadOnlyList<PassageMatchDto>>, SearchPassagesHandler>();
        app.Services.AddRequestValidator<SearchPassagesQuery, SearchPassagesQueryValidator>();
    }
}
