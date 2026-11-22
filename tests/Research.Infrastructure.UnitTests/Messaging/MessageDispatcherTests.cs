using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Research.Shared.Contracts.Search;
using Research.Shared.Contracts.Steps;
using Research.Shared.Bus.Mediation;
using Research.Shared.Bus.Messaging;
using Research.Shared.Bus.Messaging.Consuming;
using Research.Shared.Core.Actors;
using Research.Shared.Core.Logging;
using Research.Shared.Kernel.Results;
using Research.Infrastructure.UnitTests.TestSupport;
using Research.Shared.Core.Correlation;
using Research.Shared.Bus.Messaging.Observability;
using Research.Shared.Bus;

namespace Research.Infrastructure.UnitTests.Messaging;

public sealed class MessageDispatcherTests
{
    private readonly RecordingPublisher _publisher = new();
    private readonly ProbeHandler _handler = new();
    private readonly MessagingOptions _options = new() { ServiceName = "search", MaxAttempts = 3, RetryDelaysSeconds = [10, 60] };

    [Fact]
    public async Task Success_Acks_AndRunsAsSystemActor()
    {
        var result = await Dispatch(Delivery(attempt: 1), TestContext.Current.CancellationToken);

        result.ShouldBe(DispatchResult.Ack);
        _handler.Received.ShouldHaveSingleItem().Query.ShouldBe("q");
        _handler.Actor.ShouldBe("system:search");
        _handler.Context!.Consumer.ShouldBe("search.search.search-requested");
    }

    [Fact]
    public async Task Failure_WithAttemptsLeft_RetriesWithTheMatchingDelay()
    {
        _handler.Throw = new InvalidOperationException("transient");

        (await Dispatch(Delivery(attempt: 1), TestContext.Current.CancellationToken)).ShouldBe(DispatchResult.Retry(0));
        (await Dispatch(Delivery(attempt: 2), TestContext.Current.CancellationToken)).ShouldBe(DispatchResult.Retry(1));
        _publisher.Published.ShouldBeEmpty();
    }

    [Fact]
    public async Task Failure_OnLastAttempt_ReportsStepExhausted_AndDeadLetters()
    {
        _handler.Throw = new InvalidOperationException("still failing");

        var result = await Dispatch(Delivery(attempt: 3), TestContext.Current.CancellationToken);

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

        var result = await Dispatch(Delivery(attempt: 3), TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(DispatchOutcome.Retry);
    }

    [Fact]
    public async Task Poison_WithStepHeaders_ReportsPoisonMessage()
    {
        var jobId = Guid.NewGuid();
        var delivery = new IncomingDelivery(
            Guid.NewGuid(),
            Encoding.UTF8.GetBytes("{ not json"),
            new Dictionary<string, string>(StringComparer.Ordinal) { [MessageHeaders.JobId] = jobId.ToString(), [MessageHeaders.StepKey] = "search:1" },
            Attempt: 1);

        var result = await Dispatch(delivery, TestContext.Current.CancellationToken);

        result.ShouldBe(DispatchResult.DeadLetter);
        var failed = ReadStepFailed(_publisher.Published.ShouldHaveSingleItem());
        failed.ErrorCode.ShouldBe(MessagingErrors.PoisonMessage.Code);
        failed.JobId.ShouldBe(jobId);
        _handler.Received.ShouldBeEmpty();
    }

    [Fact]
    public async Task Poison_WithoutHeaders_DeadLettersSilently()
    {
        var delivery = new IncomingDelivery(Guid.NewGuid(), Encoding.UTF8.GetBytes("[]"), new Dictionary<string, string>(StringComparer.Ordinal), Attempt: 1);
        (await Dispatch(delivery, TestContext.Current.CancellationToken)).ShouldBe(DispatchResult.DeadLetter);
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

    [Fact]
    public async Task CorrelationAndProducerTrace_FlowThroughEnvelopeFactoryAndMessageDispatcherScope()
    {
        var time = new FakeTimeProvider();
        var factory = new EnvelopeFactory(time);
        var message = new SearchRequested(Guid.NewGuid(), "search:0", "q", 5);

        OutgoingEnvelope envelope;
        ActivityTraceId producerTraceId;
        using (var producerActivity = new Activity("produce").SetIdFormat(ActivityIdFormat.W3C).Start())
        using (CorrelationIdContext.Push("corr-e2e-1"))
        {
            producerTraceId = producerActivity.TraceId;
            envelope = factory.Create(message);
        }

        envelope.Headers.ShouldContainKeyAndValue(MessageHeaders.CorrelationId, "corr-e2e-1");
        envelope.Headers.ShouldContainKey(MessageHeaders.TraceParent);

        var capturedScopes = new List<IReadOnlyDictionary<string, object>>();
        string? observedCorrelationInHandler = null;
        _handler.OnHandle = () => observedCorrelationInHandler = CorrelationIdContext.Current;

        var delivery = new IncomingDelivery(
            envelope.MessageId,
            Encoding.UTF8.GetBytes(envelope.Payload),
            envelope.Headers,
            Attempt: 1);

        var result = await Dispatch(delivery, new ScopeRecordingLogger(capturedScopes), TestContext.Current.CancellationToken);

        result.ShouldBe(DispatchResult.Ack);
        observedCorrelationInHandler.ShouldBe("corr-e2e-1");
        CorrelationIdContext.Current.ShouldBeNull();
        var scope = capturedScopes.ShouldHaveSingleItem();
        scope["JobId"].ShouldBe(message.JobId);
        scope["StepKey"].ShouldBe("search:0");
        scope["CorrelationId"].ShouldBe("corr-e2e-1");
        scope["ProducerTraceId"].ShouldBe(producerTraceId.ToString());
    }

    private Task<DispatchResult> Dispatch(IncomingDelivery delivery, CancellationToken stoppingToken = default) =>
        Dispatch(delivery, logger: null, stoppingToken);

    private Task<DispatchResult> Dispatch(
        IncomingDelivery delivery,
        ILogger<MessageDispatcher<SearchRequested>>? logger,
        CancellationToken stoppingToken = default)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMetrics();
        services.AddMediation();
        services.AddScoped<CurrentActor>();
        services.AddScoped<IRequestHandler<IncomingMessageEnvelope<SearchRequested>, Unit>>(provider =>
        {
            _handler.ActorSource = provider.GetRequiredService<CurrentActor>();
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
            logger ?? NullLogger<MessageDispatcher<SearchRequested>>.Instance);

        return dispatcher.DispatchAsync(delivery, "search.search.search-requested", stoppingToken);
    }

    private static IncomingDelivery Delivery(int attempt)
    {
        var message = new SearchRequested(Guid.NewGuid(), "search:0", "q", 5);
        var body = JsonSerializer.SerializeToUtf8Bytes(message, MessageJson.Options);
        return new IncomingDelivery(Guid.NewGuid(), body, new Dictionary<string, string>(StringComparer.Ordinal), attempt);
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

        public CurrentActor? ActorSource { get; set; }

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

    private sealed class ScopeRecordingLogger(List<IReadOnlyDictionary<string, object>> scopes)
        : ILogger<MessageDispatcher<SearchRequested>>
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            if (state is IReadOnlyDictionary<string, object> dict)
            {
                scopes.Add(dict);
            }

            return NullDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
