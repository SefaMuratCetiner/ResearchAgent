using Microsoft.Extensions.DependencyInjection;
using Research.Hosting.Composition;
using Research.SharedKernel.Actors;

namespace Research.Hosting.Features;

/// <summary>
/// The current actor, one per DI scope. HTTP requests keep the default (<c>api</c>); message dispatch and worker runs
/// set <c>system:&lt;service&gt;</c> on the <see cref="ScopedActor"/> of the scope they create.
/// </summary>
public sealed class ActorsFeature : IResearchFeature
{
    public void Register(ResearchBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Services.AddScoped<ScopedActor>();
        app.Services.AddScoped<ICurrentActor>(static provider => provider.GetRequiredService<ScopedActor>());
    }
}
