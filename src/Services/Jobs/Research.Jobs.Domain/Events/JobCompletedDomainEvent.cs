using Research.SharedKernel.Events;

namespace Research.Jobs.Domain.Events;

public sealed record JobCompletedDomainEvent(Guid JobId, JobStatus Status, IReadOnlyList<string> Warnings, DateTimeOffset JobCreatedAt, DateTimeOffset OccurredAt) : IDomainEvent;
