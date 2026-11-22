using Research.Shared.Kernel.Events;

namespace Research.Jobs.Domain.Events;

public sealed record JobCreatedDomainEvent(Guid JobId, string Question, DateTimeOffset OccurredAt) : IDomainEvent;
