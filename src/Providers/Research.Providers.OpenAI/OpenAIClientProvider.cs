using System.ClientModel;
using Microsoft.Extensions.Options;
using OpenAI;

namespace Research.Providers.OpenAI;

/// <summary>
/// Provides the <see cref="OpenAIClient"/> for the current <see cref="OpenAIOptions.ApiKey"/>, rebuilding it when the
/// key rotates so a new key applies without a restart. In-flight requests finish on the previous client.
/// </summary>
public sealed class OpenAIClientProvider(IOptionsMonitor<OpenAIOptions> options)
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
