using Microsoft.Extensions.DependencyInjection;
using Research.Shared.Adapters.Events;
using Research.Shared.Core.Events;
using Research.Shared.Core.Transactions;
using Research.SharedKernel.Common;

namespace Research.Infrastructure.UnitTests.Events;

public sealed class DomainEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_InvokesAllRegisteredHandlersForMatchingEventType()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddScoped<IDomainEventHandler<PingEvent>>(_ => new RecordingHandler("first", log));
        services.AddScoped<IDomainEventHandler<PingEvent>>(_ => new RecordingHandler("second", log));
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
        await using var tx = new FakeTransactionScope();

        await dispatcher.DispatchAsync([new PingEvent("hello", DateTimeOffset.UtcNow)], tx, CancellationToken.None);

        log.ShouldBe(["first:hello", "second:hello"]);
    }

    [Fact]
    public async Task DispatchAsync_WithNoRegisteredHandlers_Succeeds()
    {
        await using var provider = new ServiceCollection().AddScoped<IDomainEventDispatcher, DomainEventDispatcher>().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
        await using var tx = new FakeTransactionScope();

        await dispatcher.DispatchAsync([new UnhandledEvent(DateTimeOffset.UtcNow)], tx, CancellationToken.None);
    }

    private sealed record PingEvent(string Message, DateTimeOffset OccurredAt) : IDomainEvent;

    private sealed record UnhandledEvent(DateTimeOffset OccurredAt) : IDomainEvent;

    private sealed class RecordingHandler(string name, List<string> log) : IDomainEventHandler<PingEvent>
    {
        public Task HandleAsync(PingEvent domainEvent, ITransactionScope transaction, CancellationToken cancellationToken)
        {
            log.Add($"{name}:{domainEvent.Message}");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTransactionScope : ITransactionScope
    {
        public void Track(BaseEntity entity)
        {
        }

        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
