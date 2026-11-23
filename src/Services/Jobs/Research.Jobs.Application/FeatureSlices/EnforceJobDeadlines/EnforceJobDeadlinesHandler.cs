using Research.Shared.Bus.Mediation;

namespace Research.Jobs.Application.EnforceJobDeadlines;

/// <summary>Enforces synthesis cutoffs and deadlines through <see cref="JobDeadlineService"/>.</summary>
public sealed class EnforceJobDeadlinesHandler(JobDeadlineService deadlines) : ICommandHandler<EnforceJobDeadlinesCommand, int>
{
    public Task<int> HandleAsync(EnforceJobDeadlinesCommand request, CancellationToken cancellationToken) =>
        deadlines.EnforceAsync(cancellationToken);
}
