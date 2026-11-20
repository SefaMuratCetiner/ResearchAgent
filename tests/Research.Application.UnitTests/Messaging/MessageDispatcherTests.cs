using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Research.Contracts.Search;
using Research.Contracts.Steps;
using Research.Shared.Adapters.Messaging;
using Research.Shared.Adapters.Messaging.Consuming;
using Research.Shared.Core.Actors;
using Research.Shared.Core.Messaging;
using Research.Application.UnitTests.TestSupport;

namespace Research.Application.UnitTests.Messaging;

public sealed class MessageDispatcherTests
{
    private readonly RecordingPublisher _publisher = new();
    private readonly ProbeHandler _handler = new();
    private readonly MessagingOptions _options = new() { ServiceName = "search", MaxAttempts = 3, RetryDelaysSeconds = [10, 60] };

    [Fact]
    public async Task Success_Acks_AndRunsAsSystemActor()
    {
        var result = await Dispatch(Delivery(attempt: 1));

        result.ShouldBe(DispatchResult.Ack);
        _handler.Received.ShouldHaveSingleItem().Query.ShouldBe("q");
        _handler.Actor.ShouldBe("system:search");
        _handler.Context!.Consumer.ShouldBe("search.search.search-requested");
    }

    [Fact]
    public async Task Failure_WithAttemptsLeft_RetriesWithTheMatchingDelay()
    {
        _handler.Throw = new InvalidOperationException("transient");

        (await Dispatch(Delivery(attempt: 1))).ShouldBe(DispatchResult.Retry(0));
        (await Dispatch(Delivery(attempt: 2))).ShouldBe(DispatchResult.Retry(1));
        _publisher.Published.ShouldBeEmpty();
    }

    [Fact]
    public async Task Failure_OnLastAttempt_ReportsStepExhausted_AndDeadLetters()
    {
        _handler.Throw = new InvalidOperationException("still failing");

        var result = await Dispatch(Delivery(attempt: 3));

        result.ShouldBe(DispatchResult.DeadLetter);
        var failed = ReadStepFailed(_publisher.Published.ShouldHaveSingleItem());
        failed.ErrorCode.ShouldBe(MessagingErrors.StepExhausted.Code);
        failed.Service.ShouldBe("search");
        failed.StepKey.ShouldBe("search:0");
    }

    [Fact]
    public async Task Exhausted_WhenStepFailedCannotBePublished_RetriesInsteadOfLosingTheAnswer()
    {
        _handler.Throw = new InvalidOperationException("still failing");
        _publisher.Fail = true;

        var result = await Dispatch(Delivery(attempt: 3));

        result.Outcome.ShouldBe(DispatchOutcome.Retry);
    }

    [Fact]
    public async Task Poison_WithStepHeaders_ReportsPoisonMessage()
    {
        var jobId = Guid.NewGuid();
        var delivery = new IncomingDelivery(
            Guid.NewGuid(),
            Encoding.UTF8.GetBytes("{ not json"),
            new Dictionary<string, string> { [MessageHeaders.JobId] = jobId.ToString(), [MessageHeaders.StepKey] = "search:1" },
            Attempt: 1);

        var result = await Dispatch(delivery);

        result.ShouldBe(DispatchResult.DeadLetter);
        var failed = ReadStepFailed(_publisher.Published.ShouldHaveSingleItem());
        failed.ErrorCode.ShouldBe(MessagingErrors.PoisonMessage.Code);
        failed.JobId.ShouldBe(jobId);
        _handler.Received.ShouldBeEmpty();
    }

    [Fact]
    public async Task Poison_WithoutHeaders_DeadLettersSilently()
    {
        var delivery = new IncomingDelivery(Guid.NewGuid(), Encoding.UTF8.GetBytes("[]"), new Dictionary<string, string>(), Attempt: 1);
        (await Dispatch(delivery)).ShouldBe(DispatchResult.DeadLetter);
        _publisher.Published.ShouldBeEmpty();
    }

    [Fact]
    public async Task Shutdown_Requeues()
    {
        using var stopping = new CancellationTokenSource();
        _handler.OnHandle = () =>
        {
            stopping.Cancel();
            throw new OperationCanceledException(stopping.Token);
        };

        (await Dispatch(Delivery(attempt: 1), stopping.Token)).ShouldBe(DispatchResult.Requeue);
    }

    private Task<DispatchResult> Dispatch(IncomingDelivery delivery, CancellationToken stoppingToken = default)
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedActor>();
        services.AddScoped<IMessageHandler<SearchRequested>>(provider =>
        {
            _handler.ActorSource = provider.GetRequiredService<ScopedActor>();
            return _handler;
        });

        var time = new FakeTimeProvider();
        var dispatcher = new MessageDispatcher<SearchRequested>(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            _publisher,
            new EnvelopeFactory(time),
            TestOptions.Of(_options),
            new MessagingMetrics(TestMeters.Create()),
            time,
            NullLogger<MessageDispatcher<SearchRequested>>.Instance);

        return dispatcher.DispatchAsync(delivery, "search.search.search-requested", stoppingToken);
    }

    private static IncomingDelivery Delivery(int attempt)
    {
        var message = new SearchRequested(Guid.NewGuid(), "search:0", "q", 5);
        var body = JsonSerializer.SerializeToUtf8Bytes(message, MessageJson.Options);
        return new IncomingDelivery(Guid.NewGuid(), body, new Dictionary<string, string>(), attempt);
    }

    private static StepFailed ReadStepFailed(OutgoingEnvelope envelope)
    {
        envelope.MessageType.ShouldBe(StepFailed.MessageType);
        return JsonSerializer.Deserialize<StepFailed>(envelope.Payload, MessageJson.Options)!;
    }

    private sealed class ProbeHandler : IMessageHandler<SearchRequested>
    {
        public List<SearchRequested> Received { get; } = [];

        public Exception? Throw { get; set; }

        public Action? OnHandle { get; set; }

        public ScopedActor? ActorSource { get; set; }

        public string? Actor { get; private set; }

        public MessageContext? Context { get; private set; }

        public Task HandleAsync(SearchRequested message, MessageContext context, CancellationToken cancellationToken)
        {
            OnHandle?.Invoke();
            if (Throw is not null)
            {
                throw Throw;
            }

            Received.Add(message);
            Actor = ActorSource?.Id;
            Context = context;
            return Task.CompletedTask;
        }
    }
}
