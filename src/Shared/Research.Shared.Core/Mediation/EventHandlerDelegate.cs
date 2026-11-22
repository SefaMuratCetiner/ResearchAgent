namespace Research.Shared.Core.Mediation;

/// <summary>Represents the next step in the domain event pipeline.</summary>
public delegate Task EventHandlerDelegate(CancellationToken cancellationToken);
