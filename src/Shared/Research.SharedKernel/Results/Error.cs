namespace Research.SharedKernel.Results;

/// <summary>
/// A stable, documented error. <see cref="Code"/> is prefixed with the service that owns it
/// (<c>jobs.job_not_found</c>); codes from shared libraries use <c>research.</c>. Codes are never renamed or reused.
/// </summary>
/// <param name="Code">The machine-readable code clients branch on.</param>
/// <param name="Kind">What kind of failure this is.</param>
/// <param name="Title">A short, human-readable summary.</param>
public sealed record Error(string Code, ErrorKind Kind, string Title)
{
    public override string ToString() => Code;
}
