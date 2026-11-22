using Research.Shared.Contracts;

namespace Research.Shared.Bus.Messaging.Consuming;

/// <summary>
/// Broker-independent consumer dispatch pipeline for one integration message contract. Transport listeners call
/// <see cref="DispatchAsync"/> once per delivery and act on the returned <see cref="DispatchResult"/>.
/// </summary>
public interface IMessageDispatcher<TMessage>
    where TMessage : class, IIntegrationMessage
{
    Task<DispatchResult> DispatchAsync(IncomingDelivery delivery, string consumer, CancellationToken stoppingToken);
}
