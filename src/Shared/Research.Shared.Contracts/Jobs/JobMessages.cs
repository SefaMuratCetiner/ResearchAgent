namespace Research.Shared.Contracts.Jobs;

/// <summary>A research job was accepted. Published for observers; no service needs it to make progress.</summary>
public sealed record JobCreated(Guid JobId, string StepKey, string Question) : IIntegrationMessage
{
    public static string MessageType => "jobs.job-created";

    public static MessageKind Kind => MessageKind.Event;
}

/// <summary>A job finished with a report (possibly with warnings).</summary>
public sealed record JobCompleted(Guid JobId, string StepKey, string Status, IReadOnlyList<string> Warnings) : IIntegrationMessage
{
    public static string MessageType => "jobs.job-completed";

    public static MessageKind Kind => MessageKind.Event;
}

/// <summary>A job ended without a report.</summary>
public sealed record JobFailed(Guid JobId, string StepKey, string ErrorCode) : IIntegrationMessage
{
    public static string MessageType => "jobs.job-failed";

    public static MessageKind Kind => MessageKind.Event;
}

/// <summary>A job was cancelled by the client.</summary>
public sealed record JobCancelled(Guid JobId, string StepKey) : IIntegrationMessage
{
    public static string MessageType => "jobs.job-cancelled";

    public static MessageKind Kind => MessageKind.Event;
}

/// <summary>A job hit its deadline before a report was ready.</summary>
public sealed record JobExpired(Guid JobId, string StepKey) : IIntegrationMessage
{
    public static string MessageType => "jobs.job-expired";

    public static MessageKind Kind => MessageKind.Event;
}
