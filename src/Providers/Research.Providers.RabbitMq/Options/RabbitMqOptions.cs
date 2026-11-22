namespace Research.Providers.RabbitMq;

/// <summary>Bound from the <c>RabbitMq</c> section. The connection itself is Aspire's (<c>ConnectionStrings:bus</c>).</summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    /// <summary>The topic exchange every contract is published to, routed by its message type.</summary>
    public string Exchange { get; set; } = "research.events";

    /// <summary>How long a stopping consumer waits for in-flight deliveries before requeueing them.</summary>
    public int ShutdownDrainSeconds { get; set; } = 30;
}
