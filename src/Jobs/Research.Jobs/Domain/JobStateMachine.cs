namespace Research.Jobs.Domain;

/// <summary>
/// The allowed transitions (§10.1). The repository's compare-and-set update takes <see cref="AllowedFrom"/>, so a
/// transition that lost a race (the job moved on meanwhile) updates nothing instead of overwriting.
/// </summary>
public static class JobStateMachine
{
    private static readonly JobStatus[] Active = [JobStatus.Planning, JobStatus.Gathering, JobStatus.Synthesizing];

    /// <summary>The states a job may be in to move to <paramref name="to"/>.</summary>
    public static IReadOnlyList<JobStatus> AllowedFrom(JobStatus to) => to switch
    {
        JobStatus.Planning => [],
        JobStatus.Gathering => [JobStatus.Planning],
        JobStatus.Synthesizing => [JobStatus.Gathering],
        JobStatus.Completed or JobStatus.CompletedWithWarnings => [JobStatus.Synthesizing],
        JobStatus.Failed or JobStatus.Cancelled or JobStatus.Expired => Active,
        _ => [],
    };

    public static bool CanMove(JobStatus from, JobStatus to) => AllowedFrom(to).Contains(from);
}
