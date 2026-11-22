using Microsoft.Extensions.DependencyInjection;
using Research.Shared.Bus.Mediation;
using Research.Shared.Bus.Events;
using Research.Shared.Kernel.Events;
using Research.Shared.Bus;

namespace Research.Infrastructure.UnitTests.Events;

public sealed class DomainEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_InvokesAllRegisteredHandlersForMatchingEventType()
    {
        var log = new List<string>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMetrics();
        services.AddMediation();
        services.AddScoped<IDomainEventHandler<PingEvent>>(_ => new RecordingHandler("first", log));
        services.AddScoped<IDomainEventHandler<PingEvent>>(_ => new RecordingHandler("second", log));

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

        await dispatcher.DispatchAsync([new PingEvent("hello", DateTimeOffset.UtcNow)], CancellationToken.None);

        log.ShouldBe(["first:hello", "second:hello"]);
    }

    [Fact]
    public async Task DispatchAsync_WithNoRegisteredHandlers_Succeeds()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMetrics();
        services.AddMediation();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

        await dispatcher.DispatchAsync([new UnhandledEvent(DateTimeOffset.UtcNow)], CancellationToken.None);
    }

    private sealed record PingEvent(string Message, DateTimeOffset OccurredAt) : IDomainEvent;

    private sealed record UnhandledEvent(DateTimeOffset OccurredAt) : IDomainEvent;

    private sealed class RecordingHandler(string name, List<string> log) : IDomainEventHandler<PingEvent>
    {
        public Task HandleAsync(PingEvent domainEvent, CancellationToken cancellationToken)
        {
            log.Add($"{name}:{domainEvent.Message}");
            return Task.CompletedTask;
        }
    }
}
