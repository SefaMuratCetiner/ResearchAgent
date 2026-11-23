namespace Research.Shared.Kernel.Events;

/// <summary>A domain fact raised by an aggregate root and published, inside the transaction, when the aggregate is saved.</summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
