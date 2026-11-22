using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Research.Shared.Core.Actors;
using Research.Shared.Core.Documents;
using Research.Shared.Bus.Messaging;

namespace Research.Infrastructure.UnitTests.TestSupport;

/// <summary>A real <see cref="IMeterFactory"/> from the DI metrics implementation.</summary>
internal static class TestMeters
{
    public static IMeterFactory Create() =>
        new ServiceCollection().AddMetrics().BuildServiceProvider().GetRequiredService<IMeterFactory>();
}

/// <summary>Records every published envelope; optionally fails.</summary>
internal sealed class RecordingPublisher : IMessagePublisher
{
    public List<OutgoingEnvelope> Published { get; } = [];

    public bool Fail { get; set; }

    public Task PublishAsync(OutgoingEnvelope envelope, CancellationToken cancellationToken)
    {
        if (Fail)
        {
            throw new InvalidOperationException("broker down");
        }

        Published.Add(envelope);
        return Task.CompletedTask;
    }
}

/// <summary>An in-memory document store.</summary>
internal sealed class InMemoryDocumentStore : IDocumentStore
{
    public Dictionary<string, string> Blobs { get; } = new(StringComparer.Ordinal);

    public Task SaveTextAsync(string key, string text, CancellationToken cancellationToken)
    {
        Blobs[key] = text;
        return Task.CompletedTask;
    }

    public Task<string?> ReadTextAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(Blobs.TryGetValue(key, out var text) ? text : null);
}

internal sealed class FixedActor(string id = "test") : ICurrentActor
{
    public string Id { get; } = id;
}

internal static class TestOptions
{
    /// <summary>Fixed options that fit any options parameter: <c>IOptions</c>, <c>IOptionsSnapshot</c> or <c>IOptionsMonitor</c>.</summary>
    public static FixedOptions<T> Of<T>(T value)
        where T : class => new(value);
}

/// <summary>Options that never change: every accessor returns the same value and no change is ever raised.</summary>
internal sealed class FixedOptions<T>(T value) : IOptionsSnapshot<T>, IOptionsMonitor<T>
    where T : class
{
    public T Value => value;

    public T CurrentValue => value;

    public T Get(string? name) => value;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
