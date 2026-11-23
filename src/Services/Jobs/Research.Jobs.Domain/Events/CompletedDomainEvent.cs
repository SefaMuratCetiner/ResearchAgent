using Research.Shared.Kernel.Events;

namespace Research.Jobs.Domain.Events;

public sealed record CompletedDomainEvent(Guid JobId, JobStatus Status, IReadOnlyList<string> Warnings, DateTimeOffset JobCreatedAt, DateTimeOffset OccurredAt) : IDomainEvent;
