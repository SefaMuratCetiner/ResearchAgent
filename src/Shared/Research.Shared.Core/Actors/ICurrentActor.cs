namespace Research.Shared.Core.Actors;

/// <summary>
/// Who performs the current operation, for audit columns. There is no authentication in this system (the gateway
/// owns identity), so HTTP requests run as <c>api</c> and background work as <c>system:&lt;service&gt;</c>.
/// </summary>
public interface ICurrentActor
{
    string Id { get; }
}
