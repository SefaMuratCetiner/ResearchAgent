using Research.SharedKernel.Events;

namespace Research.Jobs.Domain.Events;

public sealed record JobGatheringStartedDomainEvent(Guid JobId, IReadOnlyList<string> SubQuestions, DateTimeOffset OccurredAt) : IDomainEvent;
