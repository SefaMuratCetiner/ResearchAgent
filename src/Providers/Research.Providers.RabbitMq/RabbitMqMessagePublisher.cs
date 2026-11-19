using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Research.SharedKernel.Messaging;

namespace Research.Providers.RabbitMq;

/// <summary>
/// Publishes persistent messages on a channel with publisher confirmation tracking: each publish returns only after
/// the broker confirmed it, and throws when the broker nacks or returns it (mandatory routing). One channel per
/// process, guarded by a semaphore; recreated when it closes. The exchange is read once at startup: it says where
/// messages live, so the listeners keep the startup value too and a change needs a restart.
/// </summary>
public sealed class RabbitMqMessagePublisher(IConnection connection, IOptionsMonitor<RabbitMqOptions> options)
    : IMessagePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _exchange = options.CurrentValue.Exchange;
    private IChannel? _channel;

    public Task PublishAsync(OutgoingEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var properties = CreateProperties(envelope.MessageId, envelope.MessageType, envelope.OccurredAt, envelope.Headers);
        return PublishRawAsync(_exchange, envelope.MessageType, properties, Encoding.UTF8.GetBytes(envelope.Payload), cancellationToken);
    }

    /// <summary>Publishes to any exchange and routing key; listeners use it for delay queues and DLQs.</summary>
    public async Task PublishRawAsync(
        string exchange,
        string routingKey,
        BasicProperties properties,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var channel = await GetChannelAsync(cancellationToken).ConfigureAwait(false);
            await channel.BasicPublishAsync(
                exchange,
                routingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public static BasicProperties CreateProperties(
        Guid messageId,
        string messageType,
        DateTimeOffset occurredAt,
        IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        return new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = messageId.ToString("D"),
            Type = messageType,
            Timestamp = new AmqpTimestamp(occurredAt.ToUnixTimeSeconds()),
            Headers = headers.ToDictionary(static h => h.Key, static h => (object?)h.Value, StringComparer.Ordinal),
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync().ConfigureAwait(false);
        }

        _gate.Dispose();
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        if (_channel is not null)
        {
            await _channel.DisposeAsync().ConfigureAwait(false);
        }

        var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken).ConfigureAwait(false);
        await RabbitMqTopology.DeclareExchangeAsync(channel, _exchange, cancellationToken).ConfigureAwait(false);
        _channel = channel;
        return channel;
    }
}

/// <summary>Reads AMQP header values, which arrive as UTF-8 byte arrays.</summary>
internal static class AmqpHeaders
{
    public static Dictionary<string, string> ToStrings(IDictionary<string, object?>? headers)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (headers is null)
        {
            return result;
        }

        foreach (var (key, value) in headers)
        {
            var text = value switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string s => s,
                null => null,
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => null,
            };

            if (text is not null)
            {
                result[key] = text;
            }
        }

        return result;
    }
}
