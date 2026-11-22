using Research.Shared.Core.Mediation;

namespace Research.Jobs.Application.RecoverStuckJobs;

/// <summary>Re-emit or fail expired steps on active jobs; returns how many steps were handled.</summary>
public sealed record RecoverStuckJobsCommand : ICommand<int>;
