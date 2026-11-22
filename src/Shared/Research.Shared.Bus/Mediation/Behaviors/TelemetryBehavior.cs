using System.Diagnostics;
using Research.Shared.Bus.Mediation;
using Research.Shared.Kernel.Events;
using Research.Shared.Kernel.Results;
using Research.Shared.Bus.Mediation.Observability;

namespace Research.Shared.Bus.Mediation.Behaviors;

/// <summary>Records OpenTelemetry traces and metrics around every command, query, and integration message execution.</summary>
internal sealed class TelemetryBehavior<TRequest, TResponse>(
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
        if (request is IJobScopedRequest jobScoped)
        {
            activity?.SetTag("research.job.id", jobScoped.JobId.ToString("D"));
        }

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
