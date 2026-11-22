using Microsoft.Extensions.Logging;
using Research.Shared.Core.Mediation;
using Research.SharedKernel.Events;
using Research.SharedKernel.Results;

namespace Research.Shared.Adapters.Mediation.Behaviors;

/// <summary>Structured logging around every command, query, and integration message execution.</summary>
public sealed partial class LoggingBehavior<TRequest, TResponse>(
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

/// <summary>Structured logging around every domain event publication.</summary>
public sealed partial class EventLoggingBehavior<TEvent>(
    ILogger<EventLoggingBehavior<TEvent>> logger,
    TimeProvider timeProvider) : IEventPipelineBehavior<TEvent>
    where TEvent : IDomainEvent
{
    private static readonly string EventName = typeof(TEvent).Name;

    public async Task HandleAsync(TEvent domainEvent, EventHandlerDelegate next, CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        LogStarting(logger, EventName);

        try
        {
            await next(cancellationToken).ConfigureAwait(false);
            if (logger.IsEnabled(LogLevel.Debug))
            {
                var elapsedMs = timeProvider.GetElapsedTime(started).TotalMilliseconds;
                LogCompleted(logger, EventName, elapsedMs);
            }
        }
        catch (Exception exception)
        {
            LogFailed(logger, exception, EventName, timeProvider.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }

    [LoggerMessage(EventId = 1510, Level = LogLevel.Debug, Message = "Publishing domain event {EventName}")]
    private static partial void LogStarting(ILogger logger, string eventName);

    [LoggerMessage(EventId = 1511, Level = LogLevel.Debug, Message = "Published domain event {EventName} in {ElapsedMs:F1} ms")]
    private static partial void LogCompleted(ILogger logger, string eventName, double elapsedMs);

    [LoggerMessage(EventId = 1512, Level = LogLevel.Warning, Message = "Domain event {EventName} threw after {ElapsedMs:F1} ms")]
    private static partial void LogFailed(ILogger logger, Exception exception, string eventName, double elapsedMs);
}
