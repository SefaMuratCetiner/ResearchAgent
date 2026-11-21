namespace Research.SharedKernel.Common;

/// <summary>A domain event raised by an aggregate root when a meaningful business state change occurs.</summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
