using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Research.Shared.Contracts.Search;
using Research.Shared.Bus.Mediation;
using Research.Shared.Bus.Events;
using Research.Shared.Bus.Messaging;
using Research.Shared.Bus.Transactions;
using Research.Shared.Kernel.Events;
using Research.Shared.Kernel.Results;
using Research.Shared.Bus.Mediation.Observability;
using Research.Shared.Bus;

namespace Research.Infrastructure.UnitTests.Mediation;

public sealed class MediationPipelineTests
{
    private static readonly Error ValidationError = new("test.invalid", ErrorKind.Invalid, "Invalid input.");
    private static readonly Error BusinessFailure = new("test.rejected", ErrorKind.Rule, "Rejected.");

    [Fact]
    public async Task Behaviors_ExecuteInRegistrationOrder_AroundCommandHandler()
    {
        var trace = new List<string>();
        var services = CreateBaseServices();
        services.AddScoped<IPipelineBehavior<EchoCommand, Result<string>>>(_ => new TraceBehavior("outer", trace));
        services.AddScoped<IPipelineBehavior<EchoCommand, Result<string>>>(_ => new TraceBehavior("inner", trace));
        services.AddScoped<IRequestHandler<EchoCommand, Result<string>>>(_ => new EchoHandler(trace));

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(new EchoCommand("ping"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("echo:ping");
        trace.ShouldBe(["outer:before", "inner:before", "handler:ping", "inner:after", "outer:after"]);
    }

    [Fact]
    public async Task ValidationBehavior_ShortCircuits_WithTypedResultFailure_WithoutInvokingHandler()
    {
        var trace = new List<string>();
        var services = CreateBaseServices();
        services.AddScoped<IRequestValidator<EchoCommand>, NonEmptyEchoValidator>();
        services.AddScoped<IRequestHandler<EchoCommand, Result<string>>>(_ => new EchoHandler(trace));

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(new EchoCommand("   "), CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(ValidationError);
        trace.ShouldBeEmpty();
    }

    [Fact]
    public async Task TransactionBehavior_CommitsOnSuccess_AndSkipsCommitOnBusinessFailureOrException()
    {
        var uow = new RecordingUnitOfWork();
        var services = CreateBaseServices();
        services.AddScoped<IUnitOfWork>(_ => uow);
        services.AddScoped<IRequestHandler<TransactionalCommand, Result>, TransactionalHandler>();

        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var ok = await sender.SendAsync(new TransactionalCommand(ShouldFail: false, ShouldThrow: false), CancellationToken.None);
            ok.IsSuccess.ShouldBeTrue();
        }

        uow.Begun.ShouldBe(1);
        uow.Committed.ShouldBe(1);
        uow.Disposed.ShouldBe(1);

        await using (var scope = provider.CreateAsyncScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var failed = await sender.SendAsync(new TransactionalCommand(ShouldFail: true, ShouldThrow: false), CancellationToken.None);
            failed.IsSuccess.ShouldBeFalse();
            failed.Error.ShouldBe(BusinessFailure);
        }

        uow.Begun.ShouldBe(2);
        uow.Committed.ShouldBe(1);
        uow.Disposed.ShouldBe(2);

        await using (var scope = provider.CreateAsyncScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await Should.ThrowAsync<InvalidOperationException>(() =>
                sender.SendAsync(new TransactionalCommand(ShouldFail: false, ShouldThrow: true), CancellationToken.None));
        }

        uow.Begun.ShouldBe(3);
        uow.Committed.ShouldBe(1);
        uow.Disposed.ShouldBe(3);
    }

    [Fact]
    public async Task InboxBehavior_SkipsAlreadyProcessedIntegrationMessage_AndRunsNewOnes()
    {
        var inbox = new FakeInbox();
        var handled = new List<string>();
        var services = CreateBaseServices();
        services.AddScoped<IInbox>(_ => inbox);
        services.AddScoped<IRequestHandler<IncomingMessageEnvelope<SearchRequested>, Unit>>(_ => new RecordingMessageHandler(handled));

        await using var provider = services.BuildServiceProvider();
        var seenId = Guid.NewGuid();
        var freshId = Guid.NewGuid();
        inbox.Processed.Add((seenId, "search.queue"));

        using (var scope = provider.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var dupCtx = new MessageContext(seenId, SearchRequested.MessageType, 2, DateTimeOffset.UtcNow, "search.queue");
            await sender.SendAsync(new IncomingMessageEnvelope<SearchRequested>(new SearchRequested(Guid.NewGuid(), "search:0", "dup", 5), dupCtx), CancellationToken.None);

            var freshCtx = new MessageContext(freshId, SearchRequested.MessageType, 1, DateTimeOffset.UtcNow, "search.queue");
            await sender.SendAsync(new IncomingMessageEnvelope<SearchRequested>(new SearchRequested(Guid.NewGuid(), "search:1", "fresh", 5), freshCtx), CancellationToken.None);
        }

        handled.ShouldBe(["fresh"]);
    }

    [Fact]
    public async Task EventPipeline_WrapsDomainEventHandlers_AndEmitsTracingActivities()
    {
        var trace = new List<string>();
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => string.Equals(source.Name, MediationTracing.SourceName, StringComparison.Ordinal),
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Add,
        };
        ActivitySource.AddActivityListener(listener);

        var services = CreateBaseServices();
        services.AddScoped<IEventPipelineBehavior<SampleEvent>>(_ => new TraceEventBehavior(trace));
        services.AddScoped<IDomainEventHandler<SampleEvent>>(_ => new SampleEventHandler(trace));

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        await publisher.PublishAsync(new SampleEvent("ready", DateTimeOffset.UtcNow), CancellationToken.None);

        trace.ShouldBe(["event:before", "event-handler:ready", "event:after"]);
        activities.ShouldContain(static a => a.DisplayName == "event SampleEvent" && Equals(a.GetTagItem("mediation.outcome"), "ok"));
    }

    [Fact]
    public async Task TelemetryBehavior_RunsOutsideLoggingBehavior_AndScopesJobIdOnJobScopedRequests()
    {
        var recordedLogs = new List<(ActivitySpanId? SpanId, string? ActivityName, IReadOnlyList<IReadOnlyDictionary<string, object>> Scopes)>();
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => string.Equals(source.Name, MediationTracing.SourceName, StringComparison.Ordinal),
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Add,
        };
        ActivitySource.AddActivityListener(listener);

        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddProvider(new CapturingLoggerProvider(recordedLogs));
        });
        services.AddMetrics();
        services.AddMediation();
        services.AddScoped<IRequestHandler<JobScopedCommand, Result<string>>, JobScopedHandler>();

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var jobId = Guid.NewGuid();

        var result = await sender.SendAsync(new JobScopedCommand(jobId), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var mediationActivity = activities.ShouldHaveSingleItem();
        mediationActivity.GetTagItem("research.job.id").ShouldBe(jobId.ToString("D"));
        recordedLogs.ShouldNotBeEmpty();
        recordedLogs.ShouldAllBe(entry => entry.SpanId == mediationActivity.SpanId);
        recordedLogs.ShouldAllBe(entry => entry.Scopes.Any(s => s.ContainsKey("JobId") && Equals(s["JobId"], jobId)));
    }

    private static ServiceCollection CreateBaseServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMetrics();
        services.AddMediation();
        return services;
    }

    private sealed record EchoCommand(string Text) : ICommand<Result<string>>;

    private sealed class NonEmptyEchoValidator : IRequestValidator<EchoCommand>
    {
        public ValueTask<Error?> ValidateAsync(EchoCommand request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(string.IsNullOrWhiteSpace(request.Text) ? ValidationError : null);
    }

    private sealed class EchoHandler(List<string> trace) : ICommandHandler<EchoCommand, Result<string>>
    {
        public Task<Result<string>> HandleAsync(EchoCommand request, CancellationToken cancellationToken)
        {
            trace.Add("handler:" + request.Text);
            return Task.FromResult(Result<string>.Success("echo:" + request.Text));
        }
    }

    private sealed class TraceBehavior(string name, List<string> trace) : IPipelineBehavior<EchoCommand, Result<string>>
    {
        public async Task<Result<string>> HandleAsync(
            EchoCommand request,
            RequestHandlerDelegate<Result<string>> next,
            CancellationToken cancellationToken)
        {
            trace.Add(name + ":before");
            var response = await next(cancellationToken);
            trace.Add(name + ":after");
            return response;
        }
    }

    private sealed record TransactionalCommand(bool ShouldFail, bool ShouldThrow)
        : ICommand<Result>, ITransactionalCommand;

    private sealed class TransactionalHandler : ICommandHandler<TransactionalCommand, Result>
    {
        public Task<Result> HandleAsync(TransactionalCommand request, CancellationToken cancellationToken)
        {
            if (request.ShouldThrow)
            {
                throw new InvalidOperationException("boom");
            }

            return Task.FromResult(request.ShouldFail ? Result.Failure(BusinessFailure) : Result.Success());
        }
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int Begun { get; private set; }

        public int Committed { get; private set; }

        public int Disposed { get; private set; }

        public Task<IAsyncDisposable> BeginAsync(CancellationToken cancellationToken)
        {
            Begun++;
            return Task.FromResult<IAsyncDisposable>(new Lease(this));
        }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            Committed++;
            return Task.CompletedTask;
        }

        public void Track(IHasDomainEvents entity)
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed class Lease(RecordingUnitOfWork owner) : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                owner.Disposed++;
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class FakeInbox : IInbox
    {
        public HashSet<(Guid MessageId, string Consumer)> Processed { get; } = [];

        public Task<bool> HasProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken) =>
            Task.FromResult(Processed.Contains((messageId, consumer)));

        public Task<bool> TryRecordAsync(Guid messageId, string consumer, CancellationToken cancellationToken) =>
            Task.FromResult(Processed.Add((messageId, consumer)));
    }

    private sealed class RecordingMessageHandler(List<string> handled) : IMessageHandler<SearchRequested>
    {
        public Task HandleAsync(SearchRequested message, MessageContext context, CancellationToken cancellationToken)
        {
            handled.Add(message.Query);
            return Task.CompletedTask;
        }
    }

    private sealed record SampleEvent(string Value, DateTimeOffset OccurredAt) : IDomainEvent;

    private sealed class SampleEventHandler(List<string> trace) : IDomainEventHandler<SampleEvent>
    {
        public Task HandleAsync(SampleEvent domainEvent, CancellationToken cancellationToken)
        {
            trace.Add("event-handler:" + domainEvent.Value);
            return Task.CompletedTask;
        }
    }

    private sealed class TraceEventBehavior(List<string> trace) : IEventPipelineBehavior<SampleEvent>
    {
        public async Task HandleAsync(SampleEvent domainEvent, EventHandlerDelegate next, CancellationToken cancellationToken)
        {
            trace.Add("event:before");
            await next(cancellationToken);
            trace.Add("event:after");
        }
    }

    private sealed record JobScopedCommand(Guid JobId) : ICommand<Result<string>>, IJobScopedRequest;

    private sealed class JobScopedHandler : ICommandHandler<JobScopedCommand, Result<string>>
    {
        public Task<Result<string>> HandleAsync(JobScopedCommand request, CancellationToken cancellationToken) =>
            Task.FromResult(Result<string>.Success("ok"));
    }

    private sealed class CapturingLoggerProvider(
        List<(ActivitySpanId? SpanId, string? ActivityName, IReadOnlyList<IReadOnlyDictionary<string, object>> Scopes)> entries)
        : ILoggerProvider
    {
        private readonly LoggerExternalScopeProvider _scopeProvider = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(entries, _scopeProvider);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(
            List<(ActivitySpanId? SpanId, string? ActivityName, IReadOnlyList<IReadOnlyDictionary<string, object>> Scopes)> entries,
            LoggerExternalScopeProvider scopeProvider) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state)
                where TState : notnull => scopeProvider.Push(state);

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var scopes = new List<IReadOnlyDictionary<string, object>>();
                scopeProvider.ForEachScope(
                    static (scope, target) =>
                    {
                        if (scope is IReadOnlyDictionary<string, object> dict)
                        {
                            target.Add(dict);
                        }
                    },
                    scopes);
                entries.Add((Activity.Current?.SpanId, Activity.Current?.DisplayName, scopes));
            }
        }
    }
}
