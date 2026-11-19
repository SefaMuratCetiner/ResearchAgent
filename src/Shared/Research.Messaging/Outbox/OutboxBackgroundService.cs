using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.SharedKernel.Messaging;

namespace Research.Messaging.Outbox;

/// <summary>
/// Publishes committed outbox rows. Runs on every replica: the store claims rows with <c>SKIP LOCKED</c>, so replicas
/// share the work without a leader. Wakes on the store's signal (NOTIFY) or the poll interval, and drains in full
/// batches while there is a backlog. Batch size and poll interval are read on every pass, so a reload applies from
/// the next pass.
/// </summary>
public sealed partial class OutboxBackgroundService(
    IOutboxStore store,
    IMessagePublisher publisher,
    IOptionsMonitor<MessagingOptions> options,
    MessagingMetrics metrics,
    TimeProvider timeProvider,
    ILogger<OutboxBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var backoff = TimeSpan.FromMilliseconds(options.CurrentValue.OutboxPollIntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = options.CurrentValue;
            var pollInterval = TimeSpan.FromMilliseconds(settings.OutboxPollIntervalMs);
            try
            {
                var published = await store.PublishPendingAsync(publisher.PublishAsync, settings.OutboxBatchSize, stoppingToken)
                    .ConfigureAwait(false);
                metrics.OutboxPublished(published);
                backoff = pollInterval;

                if (published < settings.OutboxBatchSize)
                {
                    await store.WaitForSignalAsync(pollInterval, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // The loop must survive database and broker outages; it logs and backs off.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                metrics.OutboxFailed();
                LogDispatchFailed(logger, exception, backoff.TotalSeconds);
                try
                {
                    await Task.Delay(backoff, timeProvider, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, MaxBackoff.Ticks));
            }
        }
    }

    [LoggerMessage(EventId = 1010, Level = LogLevel.Warning, Message = "Outbox dispatch failed; retrying in {BackoffSeconds} s")]
    private static partial void LogDispatchFailed(ILogger logger, Exception exception, double backoffSeconds);
}
