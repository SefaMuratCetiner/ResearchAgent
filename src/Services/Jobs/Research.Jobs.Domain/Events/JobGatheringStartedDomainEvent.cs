using Research.Shared.Kernel.Events;

namespace Research.Jobs.Domain.Events;

public sealed record JobGatheringStartedDomainEvent(Guid JobId, IReadOnlyList<string> SubQuestions, DateTimeOffset OccurredAt) : IDomainEvent;
