using Research.SharedKernel.Events;

namespace Research.Jobs.Domain.Events;

public sealed record JobExpiredDomainEvent(Guid JobId, DateTimeOffset JobCreatedAt, DateTimeOffset OccurredAt) : IDomainEvent;
