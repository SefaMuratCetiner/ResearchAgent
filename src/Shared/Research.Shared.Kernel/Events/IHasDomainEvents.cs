namespace Research.Shared.Kernel.Events;

/// <summary>Exposes the domain events an aggregate raised since it was last saved, so the saving service can publish them.</summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
