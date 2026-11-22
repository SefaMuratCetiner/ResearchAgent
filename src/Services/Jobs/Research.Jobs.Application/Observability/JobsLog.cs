using Microsoft.Extensions.Logging;
using Research.Jobs.Domain;

namespace Research.Jobs.Application.Observability;

/// <summary>Shared structured log events for Jobs terminal transitions.</summary>
internal static partial class JobsLog
{
    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Job {JobId} finished as {Status} {ErrorCode}")]
    public static partial void JobFinished(ILogger logger, Guid jobId, JobStatus status, string? errorCode);
}
