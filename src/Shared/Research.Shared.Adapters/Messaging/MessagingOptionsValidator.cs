using Microsoft.Extensions.Options;

namespace Research.Shared.Adapters.Messaging;

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
