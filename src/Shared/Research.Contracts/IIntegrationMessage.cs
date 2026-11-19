namespace Research.Contracts;

/// <summary>
/// An integration message. Every message belongs to one research job and one step of it, which the orchestrator
/// (Jobs) uses to match replies to the step that requested them.
/// </summary>
public interface IIntegrationMessage
{
    /// <summary>The wire name (<c>planning.plan-requested</c>): routing key, queue suffix and outbox type column.</summary>
    static abstract string MessageType { get; }

    Guid JobId { get; }

    string StepKey { get; }
}
