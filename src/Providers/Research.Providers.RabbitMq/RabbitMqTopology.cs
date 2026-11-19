using System.Globalization;
using RabbitMQ.Client;

namespace Research.Providers.RabbitMq;

/// <summary>
/// Names and declarations. Every declaration is idempotent, so each replica declares what it uses at startup.
/// <list type="bullet">
/// <item>Exchange <c>research.events</c> (topic, durable); routing key = message type.</item>
/// <item>Work queue <c>{service}.{messageType}</c> (quorum), bound by the message type.</item>
/// <item>Delay queues <c>{queue}.retry.{n}s</c> (quorum, message TTL) dead-lettering back to the work queue via the
/// default exchange.</item>
/// <item>Dead-letter queue <c>{queue}.dlq</c> (quorum).</item>
/// </list>
/// </summary>
public static class RabbitMqTopology
{
    public static string QueueName(string serviceName, string messageType) => serviceName + "." + messageType;

    public static string DelayQueueName(string queue, int delaySeconds) =>
        queue + ".retry." + delaySeconds.ToString(CultureInfo.InvariantCulture) + "s";

    public static string DeadLetterQueueName(string queue) => queue + ".dlq";

    public static Task DeclareExchangeAsync(IChannel channel, string exchange, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        return channel.ExchangeDeclareAsync(
            exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
    }

    public static async Task DeclareConsumerAsync(
        IChannel channel,
        string exchange,
        string queue,
        string messageType,
        IReadOnlyList<int> retryDelaysSeconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(retryDelaysSeconds);

        await DeclareExchangeAsync(channel, exchange, cancellationToken).ConfigureAwait(false);

        await channel.QueueDeclareAsync(
            queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: Quorum(),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        await channel.QueueBindAsync(queue, exchange, messageType, arguments: null, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        foreach (var delay in retryDelaysSeconds.Distinct())
        {
            var arguments = Quorum();
            arguments["x-message-ttl"] = delay * 1000;
            arguments["x-dead-letter-exchange"] = string.Empty;
            arguments["x-dead-letter-routing-key"] = queue;
            await channel.QueueDeclareAsync(
                DelayQueueName(queue, delay),
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: arguments,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        await channel.QueueDeclareAsync(
            DeadLetterQueueName(queue),
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: Quorum(),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, object?> Quorum() => new(StringComparer.Ordinal) { ["x-queue-type"] = "quorum" };
}
