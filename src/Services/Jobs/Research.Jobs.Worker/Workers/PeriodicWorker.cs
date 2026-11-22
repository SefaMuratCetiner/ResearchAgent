using Research.Jobs.Application.Observability;
using Research.Shared.Core.Actors;

namespace Research.Jobs.Worker.Workers;

/// <summary>
/// A thin timer loop: each tick opens a DI scope running as <c>system:jobs</c>, starts a root span, and calls the
/// slice's handler. Exceptions are logged and the loop continues; the work itself lives in the module.
/// <see cref="Interval"/> is read after every tick, so a reloaded interval applies from the next tick.
/// </summary>
internal abstract partial class PeriodicWorker(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger logger)
    : BackgroundService
{
    protected abstract string Name { get; }

    protected abstract TimeSpan Interval { get; }

    protected abstract Task<int> RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await RunAsync(stoppingToken).ConfigureAwait(false);

                var interval = Interval;
                if (timer.Period != interval)
                {
                    timer.Period = interval;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        using var activity = JobsTracing.StartWorkerRun(Name);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<CurrentActor>().Id = CurrentActor.System("jobs");
            var handled = await RunOnceAsync(scope.ServiceProvider, stoppingToken).ConfigureAwait(false);
            activity?.SetTag("research.worker.handled", handled);
            if (handled > 0)
            {
                LogRun(logger, Name, handled);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // A failed run is logged; the next tick tries again.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            activity?.AddException(exception);
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error);
            LogRunFailed(logger, exception, Name);
        }
    }

    [LoggerMessage(EventId = 2090, Level = LogLevel.Information, Message = "{Worker} handled {Count} items")]
    private static partial void LogRun(ILogger logger, string worker, int count);

    [LoggerMessage(EventId = 2091, Level = LogLevel.Error, Message = "{Worker} run failed")]
    private static partial void LogRunFailed(ILogger logger, Exception exception, string worker);
}
