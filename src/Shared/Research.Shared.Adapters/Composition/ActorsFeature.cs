using Microsoft.Extensions.DependencyInjection;
using Research.Shared.Core.Actors;

namespace Research.Shared.Adapters.Composition;

/// <summary>
/// The current actor, one per DI scope. HTTP requests keep the default (<c>api</c>); message dispatch and worker runs
/// set <c>system:&lt;service&gt;</c> on the <see cref="CurrentActor"/> of the scope they create.
/// </summary>
public sealed class ActorsFeature : IHostFeature
{
    public void Register(ServiceHostBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Services.AddScoped<CurrentActor>();
        app.Services.AddScoped<ICurrentActor>(static provider => provider.GetRequiredService<CurrentActor>());
    }
}
