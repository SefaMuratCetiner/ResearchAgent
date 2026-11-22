using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Research.Shared.Messaging;
using Research.Shared.Adapters.Messaging.Observability;

namespace Research.Shared.Adapters.Messaging.Outbox;

/// <summary>
/// Publishes committed outbox rows. Runs on every replica: the store claims rows with <c>SKIP LOCKED</c>, so replicas
/// share the work without a leader. Wakes on the store's signal (NOTIFY) or the poll interval, and drains in full
/// batches while there is a backlog. A message that fails is retried after
/// <see cref="MessagingOptions.OutboxRetryDelayMs"/>, then after twice as long each time up to
/// <see cref="MessagingOptions.OutboxMaxRetryDelaySeconds"/>, and parked after
/// <see cref="MessagingOptions.OutboxMaxAttempts"/>. After an error the loop pauses for the same first delay, doubling
/// up to 30 s. The settings are read on every pass, so a reload applies from the next pass.
/// </summary>
public sealed partial class OutboxPublisherWorker(
    IOutboxStore store,
    IMessagePublisher publisher,
    IOptionsMonitor<MessagingOptions> options,
    MessagingMetrics metrics,
    TimeProvider timeProvider,
    ILogger<OutboxPublisherWorker> logger) : BackgroundService
{
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var backoff = TimeSpan.FromMilliseconds(options.CurrentValue.OutboxRetryDelayMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = options.CurrentValue;
            var pollInterval = TimeSpan.FromMilliseconds(settings.OutboxPollIntervalMs);
            var retryDelay = TimeSpan.FromMilliseconds(settings.OutboxRetryDelayMs);
            try
            {
                var retry = new OutboxRetryPolicy(
                    settings.OutboxMaxAttempts,
                    retryDelay,
                    TimeSpan.FromSeconds(settings.OutboxMaxRetryDelaySeconds));
                var published = await store.PublishPendingAsync(publisher.PublishAsync, settings.OutboxBatchSize, retry, stoppingToken)
                    .ConfigureAwait(false);
                metrics.OutboxPublished(published);
                backoff = retryDelay;

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
