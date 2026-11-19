using Anthropic;
using Microsoft.Extensions.Options;

namespace Research.Providers.Anthropic;

/// <summary>
/// Hands out the <see cref="AnthropicClient"/> for the current <see cref="AnthropicOptions.ApiKey"/>, building a new
/// one when the key changes, so a rotated key applies without a restart. Requests already running finish on the old
/// client. The key is never logged.
/// </summary>
public sealed class AnthropicClientSource(IOptionsMonitor<AnthropicOptions> options)
{
    private KeyedClient? _current;

    public AnthropicClient Current
    {
        get
        {
            var key = options.CurrentValue.ApiKey;
            var current = Volatile.Read(ref _current);
            if (current is not null && string.Equals(current.ApiKey, key, StringComparison.Ordinal))
            {
                return current.Client;
            }

            var replacement = new KeyedClient(key, new AnthropicClient { ApiKey = key });
            Interlocked.Exchange(ref _current, replacement);
            return replacement.Client;
        }
    }

    private sealed record KeyedClient(string ApiKey, AnthropicClient Client);
}
