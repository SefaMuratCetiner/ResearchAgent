using Research.Shared.Kernel.Events;

namespace Research.Jobs.Domain.Events;

public sealed record FailedDomainEvent(Guid JobId, string ErrorCode, DateTimeOffset JobCreatedAt, DateTimeOffset OccurredAt) : IDomainEvent;
