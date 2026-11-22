using System.Diagnostics;
using Research.Shared.Core.Mediation;
using Research.SharedKernel.Events;
using Research.SharedKernel.Results;

namespace Research.Shared.Adapters.Mediation.Behaviors;

/// <summary>Records OpenTelemetry traces and metrics around every command, query, and integration message execution.</summary>
public sealed class TelemetryBehavior<TRequest, TResponse>(
    MediationMetrics metrics,
    TimeProvider timeProvider) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly string RequestName = RequestMetadata.GetDisplayName<TRequest>();
    private static readonly string RequestKind = RequestMetadata.GetKind<TRequest, TResponse>();

    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        using var activity = MediationTracing.StartRequest(RequestName, RequestKind);

        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);
            var outcome = response is IOperationResult { IsSuccess: false } failed
                ? failed.Error?.Code ?? "failure"
                : "ok";

            activity?.SetTag("mediation.outcome", outcome);
            metrics.RecordRequest(RequestName, RequestKind, outcome, timeProvider.GetElapsedTime(started));
            return response;
        }
        catch (Exception exception)
        {
            activity?.AddException(exception);
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("mediation.outcome", "exception");
            metrics.RecordRequest(RequestName, RequestKind, "exception", timeProvider.GetElapsedTime(started));
            throw;
        }
    }
}

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
