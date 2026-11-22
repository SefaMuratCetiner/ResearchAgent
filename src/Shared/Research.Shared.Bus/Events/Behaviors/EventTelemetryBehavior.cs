using System.Diagnostics;
using Research.Shared.Bus.Mediation.Observability;
using Research.Shared.Bus.Mediation;
using Research.Shared.Kernel.Events;
using Research.Shared.Bus.Events;

namespace Research.Shared.Bus.Events.Behaviors;

/// <summary>Records OpenTelemetry traces and metrics around every domain event publication.</summary>
internal sealed class EventTelemetryBehavior<TEvent>(
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
