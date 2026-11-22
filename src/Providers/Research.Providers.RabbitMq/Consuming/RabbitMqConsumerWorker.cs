using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Research.Shared.Contracts;
using Research.Shared.Adapters.Messaging;
using Research.Shared.Adapters.Messaging.Consuming;
using Research.Shared.Messaging;

namespace Research.Providers.RabbitMq;

/// <summary>
/// Consumes one contract's queue on every replica (competing consumers) with manual acknowledgement. Each delivery
/// goes to <see cref="MessageDispatcher{TMessage}"/>; the verdict decides ack, retry through a delay queue, dead-letter
/// or requeue. The acknowledgement always comes after the handler's commit. On shutdown the worker stops
/// consuming, lets in-flight deliveries finish within <see cref="RabbitMqOptions.ShutdownDrainSeconds"/>, and
/// requeues the rest.
/// <para>
/// Reloading: <see cref="MessagingOptions.Prefetch"/> and <see cref="MessagingOptions.RetryDelaysSeconds"/> shape the
/// consumer and its delay queues, so when either changes the worker drains in-flight deliveries and subscribes again
/// with the new values. The queue and exchange names (<see cref="MessagingOptions.ServiceName"/>,
/// <see cref="RabbitMqOptions.Exchange"/>) say where messages live; changing them on a running system would strand
/// the messages already in the old queue, so they are read once at startup and a change only logs a warning.
/// </para>
/// </summary>
public sealed partial class RabbitMqConsumerWorker<TMessage> : BackgroundService
    where TMessage : class, IIntegrationMessage
{
    private readonly IConnection _connection;
    private readonly MessageDispatcher<TMessage> _dispatcher;
    private readonly RabbitMqMessagePublisher _publisher;
    private readonly IOptionsMonitor<MessagingOptions> _messagingOptions;
    private readonly IOptionsMonitor<RabbitMqOptions> _rabbitOptions;
    private readonly ILogger<RabbitMqConsumerWorker<TMessage>> _logger;
    private readonly SemaphoreSlim _ackGate = new(1, 1);
    private readonly string _serviceName;
    private readonly string _exchange;
    private MessagingOptions _messaging;
    private int _inFlight;

    public RabbitMqConsumerWorker(
        IConnection connection,
        MessageDispatcher<TMessage> dispatcher,
        RabbitMqMessagePublisher publisher,
        IOptionsMonitor<MessagingOptions> messagingOptions,
        IOptionsMonitor<RabbitMqOptions> rabbitOptions,
        ILogger<RabbitMqConsumerWorker<TMessage>> logger)
    {
        ArgumentNullException.ThrowIfNull(messagingOptions);
        ArgumentNullException.ThrowIfNull(rabbitOptions);
        _connection = connection;
        _dispatcher = dispatcher;
        _publisher = publisher;
        _messagingOptions = messagingOptions;
        _rabbitOptions = rabbitOptions;
        _logger = logger;
        _messaging = messagingOptions.CurrentValue;
        _serviceName = _messaging.ServiceName;
        _exchange = rabbitOptions.CurrentValue.Exchange;
    }

    private enum SessionEnd
    {
        Stopping,
        Failed,
        Reload,
    }

    private string Queue => RabbitMqTopology.QueueName(_serviceName, TMessage.MessageType);

    /// <summary>
    /// Sets up the channel and consumer, retrying with backoff while the broker is unavailable, and subscribing
    /// again at once after a reload. Once consuming, the client's automatic recovery restores the channel and
    /// consumer after a connection loss.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var backoff = TimeSpan.FromSeconds(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            var end = await ConsumeAsync(stoppingToken).ConfigureAwait(false);
            if (end == SessionEnd.Stopping)
            {
                return;
            }

            if (end == SessionEnd.Reload)
            {
                backoff = TimeSpan.FromSeconds(1);
                continue;
            }

            try
            {
                await Task.Delay(backoff, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 30));
        }
    }

    /// <summary>One subscription with one set of tuning values, until the host stops, setup fails or a reload.</summary>
    private async Task<SessionEnd> ConsumeAsync(CancellationToken stoppingToken)
    {
        // The session's values: in-flight deliveries finish with them, because the next session starts only after
        // this one drained (StopConsumingAsync).
        _messaging = _messagingOptions.CurrentValue;
        using var reload = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var messagingChanged = _messagingOptions.OnChange(changed => OnMessagingChanged(changed, reload));
        using var rabbitChanged = _rabbitOptions.OnChange(OnRabbitChanged);

        IChannel? channel = null;
        string? consumerTag = null;
        try
        {
            channel = await _connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: false,
                    publisherConfirmationTrackingEnabled: false,
                    consumerDispatchConcurrency: _messaging.Prefetch),
                stoppingToken).ConfigureAwait(false);

            await RabbitMqTopology.DeclareConsumerAsync(
                channel,
                _exchange,
                Queue,
                TMessage.MessageType,
                _messaging.RetryDelaysSeconds,
                stoppingToken).ConfigureAwait(false);

            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: _messaging.Prefetch, global: false, stoppingToken)
                .ConfigureAwait(false);

            var consumer = new AsyncEventingBasicConsumer(channel);
            var consumingChannel = channel;
            consumer.ReceivedAsync += (_, delivery) => OnReceivedAsync(consumingChannel, delivery, stoppingToken);
            consumerTag = await channel.BasicConsumeAsync(Queue, autoAck: false, consumer: consumer, cancellationToken: stoppingToken)
                .ConfigureAwait(false);

            LogConsuming(_logger, Queue, _messaging.Prefetch);
            await Task.Delay(Timeout.Infinite, reload.Token).ConfigureAwait(false);
            return SessionEnd.Stopping;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return SessionEnd.Stopping;
        }
        catch (OperationCanceledException) when (reload.IsCancellationRequested)
        {
            LogReloading(_logger, Queue);
            return SessionEnd.Reload;
        }
#pragma warning disable CA1031 // A listener must never take the host down; it logs and retries, and readiness reports the broker.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogListenerFailed(_logger, exception, Queue);
            return SessionEnd.Failed;
        }
        finally
        {
            await StopConsumingAsync(channel, consumerTag).ConfigureAwait(false);
        }
    }

    private async Task OnReceivedAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        Interlocked.Increment(ref _inFlight);
        try
        {
            var headers = AmqpHeaders.ToStrings(delivery.BasicProperties.Headers);
            var attempt = headers.TryGetValue(MessageHeaders.Attempt, out var attemptText)
                && int.TryParse(attemptText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                && parsed > 0
                    ? parsed
                    : 1;
            var messageId = Guid.TryParse(delivery.BasicProperties.MessageId, out var id) ? id : (Guid?)null;
            var body = delivery.Body.ToArray(); // The delivery's memory is only valid inside this callback.

            var result = await _dispatcher.DispatchAsync(new IncomingDelivery(messageId, body, headers, attempt), Queue, stoppingToken)
                .ConfigureAwait(false);

            switch (result.Outcome)
            {
                case DispatchOutcome.Ack:
                    await AckAsync(channel, delivery.DeliveryTag).ConfigureAwait(false);
                    break;

                case DispatchOutcome.Retry:
                    var delayIndex = Math.Clamp(result.RetryDelayIndex, 0, _messaging.RetryDelaysSeconds.Length - 1);
                    var delay = _messaging.RetryDelaysSeconds[delayIndex];
                    headers[MessageHeaders.Attempt] = (attempt + 1).ToString(CultureInfo.InvariantCulture);
                    await ForwardAsync(delivery, headers, body, RabbitMqTopology.DelayQueueName(Queue, delay)).ConfigureAwait(false);
                    await AckAsync(channel, delivery.DeliveryTag).ConfigureAwait(false);
                    break;

                case DispatchOutcome.DeadLetter:
                    await ForwardAsync(delivery, headers, body, RabbitMqTopology.DeadLetterQueueName(Queue)).ConfigureAwait(false);
                    await AckAsync(channel, delivery.DeliveryTag).ConfigureAwait(false);
                    break;

                default:
                    await NackAsync(channel, delivery.DeliveryTag, requeue: true).ConfigureAwait(false);
                    break;
            }
        }
#pragma warning disable CA1031 // Forwarding failed (broker trouble): requeue so the message is not lost.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogDeliveryFailed(_logger, exception, Queue);
            try
            {
                await NackAsync(channel, delivery.DeliveryTag, requeue: true).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // The channel is gone; the broker redelivers unacknowledged messages on its own.
            catch (Exception nackException)
#pragma warning restore CA1031
            {
                LogNackFailed(_logger, nackException, Queue);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    /// <summary>Copies the delivery to another queue through the default exchange, keeping id, type and headers.</summary>
    private Task ForwardAsync(BasicDeliverEventArgs delivery, Dictionary<string, string> headers, byte[] body, string targetQueue)
    {
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = delivery.BasicProperties.ContentType,
            MessageId = delivery.BasicProperties.MessageId,
            Type = delivery.BasicProperties.Type,
            Timestamp = delivery.BasicProperties.Timestamp,
            Headers = headers.ToDictionary(static h => h.Key, static h => (object?)h.Value, StringComparer.Ordinal),
        };

        // Not linked to the stopping token: a verdict already reached must be carried out.
        return _publisher.PublishRawAsync(string.Empty, targetQueue, properties, body, CancellationToken.None);
    }

    private async Task AckAsync(IChannel channel, ulong deliveryTag)
    {
        await _ackGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await channel.BasicAckAsync(deliveryTag, multiple: false).ConfigureAwait(false);
        }
        finally
        {
            _ackGate.Release();
        }
    }

    private async Task NackAsync(IChannel channel, ulong deliveryTag, bool requeue)
    {
        await _ackGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: requeue).ConfigureAwait(false);
        }
        finally
        {
            _ackGate.Release();
        }
    }

    private async Task StopConsumingAsync(IChannel? channel, string? consumerTag)
    {
        if (channel is null)
        {
            return;
        }

        try
        {
            if (consumerTag is not null && channel.IsOpen)
            {
                await channel.BasicCancelAsync(consumerTag).ConfigureAwait(false);
            }

            var deadline = DateTime.UtcNow.AddSeconds(_rabbitOptions.CurrentValue.ShutdownDrainSeconds);
            while (Volatile.Read(ref _inFlight) > 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100).ConfigureAwait(false);
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                var remaining = Volatile.Read(ref _inFlight);
                LogStopped(_logger, Queue, remaining);
            }
            if (channel.IsOpen)
            {
                await channel.CloseAsync().ConfigureAwait(false);
            }
        }
#pragma warning disable CA1031 // Best-effort cleanup during shutdown.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogListenerFailed(_logger, exception, Queue);
        }
        finally
        {
            await channel.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Ends the current session when a value that shapes the consumer changed, so the next one subscribes with it.
    /// A changed service name cannot apply live (it names the queue) and only logs a warning.
    /// </summary>
    private void OnMessagingChanged(MessagingOptions changed, CancellationTokenSource reload)
    {
        if (!string.Equals(changed.ServiceName, _serviceName, StringComparison.Ordinal))
        {
            LogRestartRequired(_logger, "Messaging:ServiceName", Queue);
        }

        var reshaped = changed.Prefetch != _messaging.Prefetch
            || !changed.RetryDelaysSeconds.AsSpan().SequenceEqual(_messaging.RetryDelaysSeconds);
        if (!reshaped)
        {
            return;
        }

        try
        {
            reload.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The session already ended; the next one reads the new values anyway.
        }
    }

    private void OnRabbitChanged(RabbitMqOptions changed)
    {
        if (!string.Equals(changed.Exchange, _exchange, StringComparison.Ordinal))
        {
            LogRestartRequired(_logger, "RabbitMq:Exchange", Queue);
        }
    }

    public override void Dispose()
    {
        _ackGate.Dispose();
        base.Dispose();
    }

    [LoggerMessage(EventId = 8100, Level = LogLevel.Information, Message = "Consuming {Queue} with prefetch {Prefetch}")]
    private static partial void LogConsuming(ILogger logger, string queue, int prefetch);

    [LoggerMessage(EventId = 8101, Level = LogLevel.Error, Message = "The listener of {Queue} failed")]
    private static partial void LogListenerFailed(ILogger logger, Exception exception, string queue);

    [LoggerMessage(EventId = 8102, Level = LogLevel.Error, Message = "Settling a delivery on {Queue} failed; requeueing it")]
    private static partial void LogDeliveryFailed(ILogger logger, Exception exception, string queue);

    [LoggerMessage(EventId = 8103, Level = LogLevel.Information, Message = "Stopped consuming {Queue}; {InFlight} deliveries left to the broker")]
    private static partial void LogStopped(ILogger logger, string queue, int inFlight);
    [LoggerMessage(EventId = 8104, Level = LogLevel.Information, Message = "Messaging settings changed; resubscribing to {Queue} with the new values")]
    private static partial void LogReloading(ILogger logger, string queue);

    [LoggerMessage(EventId = 8105, Level = LogLevel.Warning, Message = "{Setting} changed, but it names where messages live; {Queue} keeps the startup value until the service restarts")]
    private static partial void LogRestartRequired(ILogger logger, string setting, string queue);

    [LoggerMessage(EventId = 8106, Level = LogLevel.Debug, Message = "Requeueing a failed delivery on {Queue} failed; the broker will redeliver unacknowledged messages")]
    private static partial void LogNackFailed(ILogger logger, Exception exception, string queue);
}
