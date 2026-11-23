using Research.Shared.Kernel.Events;

namespace Research.Jobs.Domain.Events;

public sealed record SynthesisStartedDomainEvent(Guid JobId, string Question, IReadOnlyList<string> Plan, DateTimeOffset OccurredAt) : IDomainEvent;
