using System.ClientModel;
using Microsoft.Extensions.Options;
using OpenAI;

namespace Research.Providers.OpenAI;

/// <summary>
/// Hands out the <see cref="OpenAIClient"/> for the current <see cref="OpenAIOptions.ApiKey"/>, building a new one when
/// the key changes, so a rotated key applies without a restart. Requests already running finish on the old client.
/// The key is never logged.
/// </summary>
public sealed class OpenAIClientSource(IOptionsMonitor<OpenAIOptions> options)
{
    private KeyedClient? _current;

    public OpenAIClient Current
    {
        get
        {
            var key = options.CurrentValue.ApiKey;
            var current = Volatile.Read(ref _current);
            if (current is not null && string.Equals(current.ApiKey, key, StringComparison.Ordinal))
            {
                return current.Client;
            }

            var replacement = new KeyedClient(key, new OpenAIClient(new ApiKeyCredential(key)));
            Interlocked.Exchange(ref _current, replacement);
            return replacement.Client;
        }
    }

    private sealed record KeyedClient(string ApiKey, OpenAIClient Client);
}
