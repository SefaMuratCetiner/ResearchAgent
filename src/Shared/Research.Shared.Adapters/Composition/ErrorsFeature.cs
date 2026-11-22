using Microsoft.Extensions.DependencyInjection;
using Research.Shared.Core.Errors;
using Research.SharedKernel.Results;

namespace Research.Shared.Adapters.Composition;

/// <summary>
/// The error catalog of this service, validated while composing: a duplicate or wrongly prefixed code fails startup.
/// The host passes the <see cref="ErrorSet"/> of every library it composes (for example, <c>JobsErrors.All</c>).
/// </summary>
public sealed class ErrorsFeature(params ErrorSet[] sets) : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Services.AddSingleton(new ErrorCatalog(sets));
    }
}
