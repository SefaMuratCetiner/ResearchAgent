using Research.SharedKernel.Events;

namespace Research.Jobs.Domain.Events;

public sealed record JobCreatedDomainEvent(Guid JobId, string Question, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record JobGatheringStartedDomainEvent(Guid JobId, IReadOnlyList<string> SubQuestions, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record JobSynthesisStartedDomainEvent(Guid JobId, string Question, IReadOnlyList<string> Plan, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record JobCompletedDomainEvent(Guid JobId, JobStatus Status, IReadOnlyList<string> Warnings, DateTimeOffset JobCreatedAt, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record JobCancelledDomainEvent(Guid JobId, DateTimeOffset JobCreatedAt, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record JobFailedDomainEvent(Guid JobId, string ErrorCode, DateTimeOffset JobCreatedAt, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record JobExpiredDomainEvent(Guid JobId, DateTimeOffset JobCreatedAt, DateTimeOffset OccurredAt) : IDomainEvent;
