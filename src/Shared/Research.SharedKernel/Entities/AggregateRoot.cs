using Research.SharedKernel.Events;

namespace Research.SharedKernel.Entities;

/// <summary>
/// Consistency boundary and transactional entry point of an aggregate. Owns state transition invariants and raises
/// uncommitted <see cref="IDomainEvent"/>s via <see cref="Raise"/>.
/// </summary>
public abstract class AggregateRoot : Entity, IHasDomainEvents
{
    private List<IDomainEvent>? _domainEvents;

    public IReadOnlyCollection<IDomainEvent> DomainEvents =>
        _domainEvents is null ? [] : _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        (_domainEvents ??= []).Add(domainEvent);
    }

    public void ClearDomainEvents() => _domainEvents?.Clear();
}
