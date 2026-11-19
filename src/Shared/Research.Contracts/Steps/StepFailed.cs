namespace Research.Contracts.Steps;

/// <summary>
/// A worker service gave up on a step: a business failure (the page is blocked, the model refused) or exhausted
/// retries. Jobs decides what that means for the job.
/// </summary>
/// <param name="ErrorCode">A documented error code (<c>fetching.fetch_disallowed</c>, <c>research.step_exhausted</c>).</param>
/// <param name="Service">The service that gave up.</param>
public sealed record StepFailed(Guid JobId, string StepKey, string ErrorCode, string Service) : IIntegrationMessage
{
    public static string MessageType => "research.step-failed";
}
