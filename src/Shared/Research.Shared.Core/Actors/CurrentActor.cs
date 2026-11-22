namespace Research.Shared.Core.Actors;

/// <summary>
/// The actor of one DI scope. Registered scoped; the code that creates a scope (a message listener, a worker run)
/// sets <see cref="Id"/> explicitly before resolving anything that writes audit fields.
/// </summary>
public sealed class CurrentActor : ICurrentActor
{
    /// <summary>The actor every HTTP request runs as.</summary>
    public const string Api = "api";

    public string Id { get; set; } = Api;

    /// <summary>The actor id of background work in <paramref name="service"/>.</summary>
    public static string System(string service) => "system:" + service;
}
