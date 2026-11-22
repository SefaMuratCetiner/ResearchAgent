using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Research.Contracts.Jobs;
using Research.Contracts.Planning;
using Research.Contracts.Search;
using Research.Contracts.Steps;
using Research.Shared.Adapters.Messaging;
using Research.Providers.RabbitMq;
using Research.Shared.Core.Actors;
using Research.Shared.Messaging;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace Research.Infrastructure.IntegrationTests.RabbitMq;

/// <summary>
/// The production listener, topology and dispatcher against a real broker: a transient failure goes through the
/// delay queue and succeeds on the next attempt; exhausted attempts publish <c>StepFailed</c> and dead-letter; a command
/// nobody can receive fails, an event nobody receives does not.
/// </summary>
public sealed class RabbitMqPipelineTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly RabbitMqContainer _container = new RabbitMqBuilder().Build();
    private IConnection _connection = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        var factory = new ConnectionFactory { Uri = new Uri(_container.GetConnectionString()) };
        _connection = await factory.CreateConnectionAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task TransientFailure_IsRetriedThroughTheDelayQueue()
    {
        var script = new Script { FailuresBeforeSuccess = 1 };
        using var host = BuildHost("retrytest", maxAttempts: 3, script);
        await host.StartAsync(TestContext.Current.CancellationToken);
        await WaitForQueueAsync(RabbitMqTopology.QueueName("retrytest", SearchRequested.MessageType));

        await PublishAsync(host, new SearchRequested(Guid.NewGuid(), "search:0", "q", 3));

        var succeeded = await script.Succeeded.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        succeeded.Attempt.ShouldBe(2);
        script.Attempts.ShouldBe(2);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UnroutableCommand_Fails_UnroutedEvent_IsConfirmed()
    {
        // The host is not started, so no queue exists: nothing can receive either message.
        using var host = BuildHost("routingtest", maxAttempts: 1, new Script());

        await PublishAsync(host, new JobCreated(Guid.NewGuid(), "job", "q"));
        await Should.ThrowAsync<RabbitMQ.Client.Exceptions.PublishReturnException>(
            () => PublishAsync(host, new PlanRequested(Guid.NewGuid(), "plan", "q", 3)));
    }

    [Fact]
    public async Task ExhaustedAttempts_PublishStepFailed_AndDeadLetter()
    {
        var script = new Script { FailuresBeforeSuccess = int.MaxValue };
        using var host = BuildHost("exhaustedtest", maxAttempts: 2, script);
        await host.StartAsync(TestContext.Current.CancellationToken);
        var queue = RabbitMqTopology.QueueName("exhaustedtest", SearchRequested.MessageType);
        await WaitForQueueAsync(queue);
        await WaitForQueueAsync(RabbitMqTopology.QueueName("exhaustedtest", StepFailed.MessageType));

        var jobId = Guid.NewGuid();
        await PublishAsync(host, new SearchRequested(jobId, "search:4", "q", 3));

        var failed = await script.StepFailed.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        failed.JobId.ShouldBe(jobId);
        failed.StepKey.ShouldBe("search:4");
        failed.ErrorCode.ShouldBe(MessagingErrors.StepExhausted.Code);
        script.Attempts.ShouldBe(2);

        await using var channel = await _connection.CreateChannelAsync();
        var deadLetters = await WaitForCountAsync(channel, RabbitMqTopology.DeadLetterQueueName(queue), 1);
        deadLetters.ShouldBe(1u);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private IHost BuildHost(string serviceName, int maxAttempts, Script script)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(_connection);
        builder.Services.AddScoped<ScopedActor>();
        builder.Services.AddSingleton(script);
        builder.Services.Configure<MessagingOptions>(options =>
        {
            options.ServiceName = serviceName;
            options.MaxAttempts = maxAttempts;
            options.RetryDelaysSeconds = [1];
            options.Prefetch = 2;
            options.MessageTimeoutSeconds = 30;
        });
        builder.Services.Configure<RabbitMqOptions>(options => options.ShutdownDrainSeconds = 5);

        RabbitMqServiceCollectionExtensions.AddPublisher(builder.Services);
        RabbitMqServiceCollectionExtensions.AddConsumer<SearchRequested, ScriptedHandler>(builder.Services);
        RabbitMqServiceCollectionExtensions.AddConsumer<StepFailed, StepFailedProbe>(builder.Services);
        return builder.Build();
    }

    private static Task PublishAsync<TMessage>(IHost host, TMessage message)
        where TMessage : Research.Contracts.IIntegrationMessage
    {
        var envelope = host.Services.GetRequiredService<EnvelopeFactory>().Create(message);
        return host.Services.GetRequiredService<IMessagePublisher>().PublishAsync(envelope, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The listener declares and binds its queue after start; publishing before that would drop the message. A quorum
    /// queue is visible before its declaration completes, so wait for the listener's consumer, which is attached only
    /// after the binding.
    /// </summary>
    private async Task WaitForQueueAsync(string queue)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (true)
        {
            try
            {
                await using var channel = await _connection.CreateChannelAsync();
                var declared = await channel.QueueDeclarePassiveAsync(queue);
                if (declared.ConsumerCount > 0)
                {
                    return;
                }
            }
            catch (RabbitMQ.Client.Exceptions.OperationInterruptedException) when (DateTime.UtcNow < deadline)
            {
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("No consumer on " + queue);
            }

            await Task.Delay(200, TestContext.Current.CancellationToken);
        }
    }

    private static async Task<uint> WaitForCountAsync(IChannel channel, string queue, uint expected)
    {
        var deadline = DateTime.UtcNow + Timeout;
        uint count;
        do
        {
            count = await channel.MessageCountAsync(queue);
            if (count >= expected)
            {
                break;
            }

            await Task.Delay(200, TestContext.Current.CancellationToken);
        }
        while (DateTime.UtcNow < deadline);

        return count;
    }

    public sealed class Script
    {
        private int _attempts;

        public int FailuresBeforeSuccess { get; init; }

        public int Attempts => Volatile.Read(ref _attempts);

        public TaskCompletionSource<MessageContext> Succeeded { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<StepFailed> StepFailed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConcurrentBag<Guid> MessageIds { get; } = [];

        public int NextAttempt() => Interlocked.Increment(ref _attempts);
    }

    public sealed class ScriptedHandler(Script script) : IMessageHandler<SearchRequested>
    {
        public Task HandleAsync(SearchRequested message, MessageContext context, CancellationToken cancellationToken)
        {
            script.MessageIds.Add(context.MessageId);
            if (script.NextAttempt() <= script.FailuresBeforeSuccess)
            {
                throw new InvalidOperationException("transient failure " + context.Attempt);
            }

            script.Succeeded.TrySetResult(context);
            return Task.CompletedTask;
        }
    }

    public sealed class StepFailedProbe(Script script) : IMessageHandler<StepFailed>
    {
        public Task HandleAsync(StepFailed message, MessageContext context, CancellationToken cancellationToken)
        {
            script.StepFailed.TrySetResult(message);
            return Task.CompletedTask;
        }
    }
}
