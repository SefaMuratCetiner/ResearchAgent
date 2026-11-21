namespace Research.SharedKernel.Events;

/// <summary>A domain fact raised by an aggregate root and dispatched inside the unit of work right before commit.</summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
