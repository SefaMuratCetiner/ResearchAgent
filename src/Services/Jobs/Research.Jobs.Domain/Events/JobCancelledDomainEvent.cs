using Research.SharedKernel.Events;

namespace Research.Jobs.Domain.Events;

public sealed record JobCancelledDomainEvent(Guid JobId, DateTimeOffset JobCreatedAt, DateTimeOffset OccurredAt) : IDomainEvent;
