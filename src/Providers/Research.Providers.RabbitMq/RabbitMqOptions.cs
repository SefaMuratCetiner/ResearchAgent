using Microsoft.Extensions.Options;

namespace Research.Providers.RabbitMq;

/// <summary>Bound from the <c>RabbitMq</c> section. The connection itself is Aspire's (<c>ConnectionStrings:bus</c>).</summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    /// <summary>The topic exchange every contract is published to, routed by its message type.</summary>
    public string Exchange { get; set; } = "research.events";

    /// <summary>How long a stopping listener waits for in-flight deliveries before requeueing them.</summary>
    public int ShutdownDrainSeconds { get; set; } = 30;
}

/// <summary>Checks <see cref="RabbitMqOptions"/> at startup.</summary>
public sealed class RabbitMqOptionsValidator : IValidateOptions<RabbitMqOptions>
{
    public ValidateOptionsResult Validate(string? name, RabbitMqOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Exchange))
        {
            failures.Add("RabbitMq:Exchange is required.");
        }

        if (options.ShutdownDrainSeconds < 0)
        {
            failures.Add("RabbitMq:ShutdownDrainSeconds cannot be negative.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
