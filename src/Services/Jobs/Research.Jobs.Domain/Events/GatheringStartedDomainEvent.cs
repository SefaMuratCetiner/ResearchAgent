using Research.Shared.Kernel.Events;

namespace Research.Jobs.Domain.Events;

public sealed record GatheringStartedDomainEvent(Guid JobId, IReadOnlyList<string> SubQuestions, DateTimeOffset OccurredAt) : IDomainEvent;
