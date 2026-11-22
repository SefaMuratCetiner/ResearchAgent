using Anthropic;
using Microsoft.Extensions.Options;

namespace Research.Providers.AnthropicWebSearch;

/// <summary>
/// Provides the <see cref="AnthropicClient"/> for the current <see cref="AnthropicWebSearchOptions.ApiKey"/>,
/// rebuilding it when the key rotates so a new key applies without a restart. In-flight searches finish on the
/// previous client.
/// </summary>
public sealed class AnthropicWebSearchClientProvider(IOptionsMonitor<AnthropicWebSearchOptions> options)
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
