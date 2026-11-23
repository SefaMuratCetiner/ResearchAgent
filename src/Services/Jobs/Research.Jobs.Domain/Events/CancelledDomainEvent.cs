using Research.Shared.Kernel.Events;

namespace Research.Jobs.Domain.Events;

public sealed record CancelledDomainEvent(Guid JobId, DateTimeOffset JobCreatedAt, DateTimeOffset OccurredAt) : IDomainEvent;
