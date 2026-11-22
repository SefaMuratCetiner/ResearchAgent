using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Research.Knowledge.Application.IndexDocument;
using Research.Knowledge.Application.Observability;
using Research.Knowledge.Application.SearchPassages;
using Research.Shared.Bus.Mediation;
using Research.Shared.Kernel.Results;

namespace Research.Knowledge.Application;

/// <summary>DI registrations for the Knowledge Application layer.</summary>
public static class KnowledgeApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddKnowledgeApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<KnowledgeMetrics>();
        services.AddScoped<IRequestHandler<PrepareDocumentCommand, Result<PreparedDocument>>, PrepareDocumentHandler>();
        services.AddScoped<IRequestHandler<SearchPassagesQuery, Result<IReadOnlyList<PassageMatchDto>>>, SearchPassagesHandler>();
        services.AddScoped<IRequestValidator<SearchPassagesQuery>, SearchPassagesQueryValidator>();

        return services;
    }
}
