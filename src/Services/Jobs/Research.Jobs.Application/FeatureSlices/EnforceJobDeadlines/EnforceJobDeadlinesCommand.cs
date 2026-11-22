using Research.Shared.Bus.Mediation;

namespace Research.Jobs.Application.EnforceJobDeadlines;

/// <summary>Enforce synthesis cutoffs and hard deadlines on active jobs; returns how many jobs changed state.</summary>
public sealed record EnforceJobDeadlinesCommand : ICommand<int>;
