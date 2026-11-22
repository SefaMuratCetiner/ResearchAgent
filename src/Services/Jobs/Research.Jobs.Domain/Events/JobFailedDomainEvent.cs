using Research.SharedKernel.Events;

namespace Research.Jobs.Domain.Events;

public sealed record JobFailedDomainEvent(Guid JobId, string ErrorCode, DateTimeOffset JobCreatedAt, DateTimeOffset OccurredAt) : IDomainEvent;
