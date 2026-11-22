namespace Research.Shared.Bus.Events;

/// <summary>Represents the next step in the domain event pipeline.</summary>
public delegate Task EventHandlerDelegate(CancellationToken cancellationToken);
