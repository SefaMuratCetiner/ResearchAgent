using Research.SharedKernel.Events;

namespace Research.Jobs.Domain.Events;

public sealed record JobSynthesisStartedDomainEvent(Guid JobId, string Question, IReadOnlyList<string> Plan, DateTimeOffset OccurredAt) : IDomainEvent;
