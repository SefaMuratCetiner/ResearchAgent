using Microsoft.Extensions.Logging;
using Research.Shared.Bus.Mediation;
using Research.Shared.Kernel.Events;
using Research.Shared.Kernel.Results;

namespace Research.Shared.Bus.Mediation.Behaviors;

/// <summary>Structured logging around every command, query, and integration message execution.</summary>
internal sealed partial class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    TimeProvider timeProvider) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly string RequestName = RequestMetadata.GetDisplayName<TRequest>();

    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        using var jobScope = request is IJobScopedRequest jobScoped
            ? logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal) { ["JobId"] = jobScoped.JobId })
            : null;
        LogStarting(logger, RequestName);

        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);
            var elapsedMs = timeProvider.GetElapsedTime(started).TotalMilliseconds;

            if (response is IOperationResult { IsSuccess: false } failed)
            {
                LogBusinessFailure(logger, RequestName, failed.Error?.Code ?? "unknown", elapsedMs);
            }
            else
            {
                LogCompleted(logger, RequestName, elapsedMs);
            }

            return response;
        }
        catch (Exception exception)
        {
            LogFailed(logger, exception, RequestName, timeProvider.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }

    [LoggerMessage(EventId = 1500, Level = LogLevel.Debug, Message = "Handling request {RequestName}")]
    private static partial void LogStarting(ILogger logger, string requestName);

    [LoggerMessage(EventId = 1501, Level = LogLevel.Debug, Message = "Handled request {RequestName} in {ElapsedMs:F1} ms")]
    private static partial void LogCompleted(ILogger logger, string requestName, double elapsedMs);

    [LoggerMessage(EventId = 1502, Level = LogLevel.Information, Message = "Request {RequestName} returned failure {ErrorCode} in {ElapsedMs:F1} ms")]
    private static partial void LogBusinessFailure(ILogger logger, string requestName, string errorCode, double elapsedMs);

    [LoggerMessage(EventId = 1503, Level = LogLevel.Warning, Message = "Request {RequestName} threw after {ElapsedMs:F1} ms")]
    private static partial void LogFailed(ILogger logger, Exception exception, string requestName, double elapsedMs);
}
