namespace Research.Shared.Kernel.Events;

/// <summary>Exposes uncommitted domain events raised during a unit of work so they can be dispatched before commit.</summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
