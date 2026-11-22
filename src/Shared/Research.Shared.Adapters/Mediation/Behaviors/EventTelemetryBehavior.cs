using System.Diagnostics;
using Research.Shared.Adapters.Mediation.Observability;
using Research.Shared.Core.Mediation;
using Research.Shared.Kernel.Events;

namespace Research.Shared.Adapters.Mediation.Behaviors;

/// <summary>Records OpenTelemetry traces and metrics around every domain event publication.</summary>
public sealed class EventTelemetryBehavior<TEvent>(
    MediationMetrics metrics,
    TimeProvider timeProvider) : IEventPipelineBehavior<TEvent>
    where TEvent : IDomainEvent
{
    private static readonly string EventName = typeof(TEvent).Name;

    public async Task HandleAsync(TEvent domainEvent, EventHandlerDelegate next, CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        using var activity = MediationTracing.StartEvent(EventName);

        try
        {
            await next(cancellationToken).ConfigureAwait(false);
            activity?.SetTag("mediation.outcome", "ok");
            metrics.RecordEvent(EventName, "ok", timeProvider.GetElapsedTime(started));
        }
        catch (Exception exception)
        {
            activity?.AddException(exception);
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("mediation.outcome", "exception");
            metrics.RecordEvent(EventName, "exception", timeProvider.GetElapsedTime(started));
            throw;
        }
    }
}
