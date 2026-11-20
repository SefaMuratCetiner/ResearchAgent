using Microsoft.Extensions.DependencyInjection;
using Research.ServiceDefaults.Composition;
using Research.ServiceDefaults.Errors;
using Research.Shared.Core.Errors;

namespace Research.ServiceDefaults.Features;

/// <summary>
/// The error catalog of this service, validated while composing: a duplicate or wrongly prefixed code fails startup.
/// The Host lists the contributors of every library it composes.
/// </summary>
public sealed class ErrorsFeature(params IErrorCatalogContributor[] contributors) : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Services.AddSingleton(new ErrorCatalog(contributors));
    }
}
