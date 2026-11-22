using Microsoft.Extensions.Logging;
using Research.Shared.Bus.Mediation;
using Research.Shared.Kernel.Events;
using Research.Shared.Bus.Events;

namespace Research.Shared.Bus.Events.Behaviors;

/// <summary>Structured logging around every domain event publication.</summary>
internal sealed partial class EventLoggingBehavior<TEvent>(
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
