namespace Research.Shared.Bus.Mediation;

/// <summary>
/// Opt-in marker for requests tied to a specific research job. <c>TelemetryBehavior</c> tags the mediation span with
/// <c>research.job.id</c> and <c>LoggingBehavior</c> opens a <c>JobId</c> log scope around the handler.
/// </summary>
public interface IJobScopedRequest
{
    Guid JobId { get; }
}
