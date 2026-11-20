using Microsoft.Extensions.Options;

namespace Research.Shared.Adapters.Messaging;

/// <summary>Bound from the <c>Messaging</c> section.</summary>
public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    /// <summary>The service name: queue prefix, actor id (<c>system:&lt;name&gt;</c>) and <c>StepFailed.Service</c>.</summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>Deliveries per message before it is dead-lettered (first attempt included).</summary>
    public int MaxAttempts { get; set; } = 4;

    /// <summary>Delay before each retry; retry n uses entry n-1 (the last entry repeats). One delay queue per entry.</summary>
    public int[] RetryDelaysSeconds { get; set; } = [10, 60, 300];

    /// <summary>Unacknowledged deliveries per consumer: the per-replica concurrency of one queue.</summary>
    public ushort Prefetch { get; set; } = 8;

    public int OutboxBatchSize { get; set; } = 100;

    /// <summary>Fallback poll when no NOTIFY arrives.</summary>
    public int OutboxPollIntervalMs { get; set; } = 60_000;

    /// <summary>
    /// First retry delay of a message whose publish failed, and the first pause of the outbox loop after an error; both
    /// double from here.
    /// </summary>
    public int OutboxRetryDelayMs { get; set; } = 1000;

    /// <summary>Failed publishes of one outbox message before it is parked (about an hour with the defaults).</summary>
    public int OutboxMaxAttempts { get; set; } = 20;

    /// <summary>Upper bound of the doubling retry delay of a failed outbox message.</summary>
    public int OutboxMaxRetryDelaySeconds { get; set; } = 300;

    /// <summary>Longest a handler may run; above the longest LLM call, below the broker's consumer timeout.</summary>
    public int MessageTimeoutSeconds { get; set; } = 900;
}

/// <summary>Checks <see cref="MessagingOptions"/> at startup.</summary>
public sealed class MessagingOptionsValidator : IValidateOptions<MessagingOptions>
{
    public ValidateOptionsResult Validate(string? name, MessagingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ServiceName))
        {
            failures.Add("Messaging:ServiceName is required.");
        }

        if (options.MaxAttempts < 1)
        {
            failures.Add("Messaging:MaxAttempts must be at least 1.");
        }

        if (options.RetryDelaysSeconds is null || options.RetryDelaysSeconds.Length == 0 || options.RetryDelaysSeconds.Any(d => d <= 0))
        {
            failures.Add("Messaging:RetryDelaysSeconds needs at least one positive delay.");
        }

        if (options.Prefetch == 0)
        {
            failures.Add("Messaging:Prefetch must be positive.");
        }

        if (options.OutboxBatchSize < 1)
        {
            failures.Add("Messaging:OutboxBatchSize must be positive.");
        }

        if (options.OutboxPollIntervalMs < 50)
        {
            failures.Add("Messaging:OutboxPollIntervalMs must be at least 50.");
        }

        if (options.OutboxRetryDelayMs < 50)
        {
            failures.Add("Messaging:OutboxRetryDelayMs must be at least 50.");
        }

        if (options.OutboxMaxAttempts < 1)
        {
            failures.Add("Messaging:OutboxMaxAttempts must be at least 1.");
        }

        if (options.OutboxMaxRetryDelaySeconds < 1)
        {
            failures.Add("Messaging:OutboxMaxRetryDelaySeconds must be positive.");
        }

        if (options.MessageTimeoutSeconds < 1)
        {
            failures.Add("Messaging:MessageTimeoutSeconds must be positive.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
