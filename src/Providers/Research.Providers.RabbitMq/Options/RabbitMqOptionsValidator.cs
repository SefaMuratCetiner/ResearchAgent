using Microsoft.Extensions.Options;

namespace Research.Providers.RabbitMq;

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
