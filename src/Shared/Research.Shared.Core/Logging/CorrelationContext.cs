namespace Research.Shared.Core.Logging;

/// <summary>
/// Ambient caller correlation id (<c>X-Correlation-ID</c>) flowing across async boundaries within a single process
/// (HTTP middleware → message outbox envelopes, or message consumer → outgoing messages and HTTP calls).
/// </summary>
public static class CorrelationContext
{
    public const string HeaderName = "X-Correlation-ID";
    public const int MaxLength = 64;

    private static readonly AsyncLocal<string?> CurrentId = new();

    /// <summary>The validated correlation id for the current async execution flow, or <see langword="null"/> if none.</summary>
    public static string? Current => CurrentId.Value;

    /// <summary>Returns <see langword="true"/> when <paramref name="value"/> is a non-empty, well-formed correlation id.</summary>
    public static bool IsValid(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.Length <= MaxLength
        && value.All(static c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');

    /// <summary>Sets <see cref="Current"/> for the lifetime of the returned scope and restores the prior value on dispose.</summary>
    public static IDisposable Push(string correlationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(correlationId);
        var previous = CurrentId.Value;
        CurrentId.Value = correlationId;
        return new Scope(previous);
    }

    private sealed class Scope(string? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CurrentId.Value = previous;
        }
    }
}
