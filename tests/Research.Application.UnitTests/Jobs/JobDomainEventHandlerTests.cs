using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Research.Application.UnitTests.TestSupport;
using Research.Contracts;
using Research.Contracts.Jobs;
using Research.Contracts.Planning;
using Research.Contracts.Search;
using Research.Contracts.Synthesis;
using Research.Jobs.Application;
using Research.Jobs.Application.AdvanceOnPlanReady;
using Research.Jobs.Application.CancelResearchJob;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Application.Common.Services;
using Research.Jobs.Application.CompleteOnReportReady;
using Research.Jobs.Application.CreateResearchJob;
using Research.Jobs.Application.EnforceJobDeadlines;
using Research.Jobs.Application.FailResearchJob;
using Research.Jobs.Application.Observability;
using Research.Jobs.Application.StartSynthesis;
using Research.Jobs.Domain;
using Research.Jobs.Domain.Events;
using Research.Shared.Messaging;

namespace Research.Application.UnitTests.Jobs;

public sealed class JobDomainEventHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task JobCreated_EmitsPlanRequestedAndEnqueuesJobCreated()
    {
        var steps = new FakeStepRepository();
        var outbox = new FakeOutbox();
        var handler = new JobCreatedDomainEventHandler(
            CreateStepDispatcher(steps, outbox),
            outbox,
            new FakeEnvelopeFactory(),
            new JobsMetrics(TestMeters.Create()),
            TestOptions.Of(new JobsOptions()));

        var jobId = Guid.NewGuid();
        await handler.HandleAsync(new JobCreatedDomainEvent(jobId, "What is pgvector?", T0), CancellationToken.None);

        steps.Inserted.Select(static s => s.StepKey).ShouldBe([JobStepKeys.Plan]);
        outbox.Enqueued.Select(static e => e.MessageType).ShouldBe([PlanRequested.MessageType, JobCreated.MessageType]);
    }

    [Fact]
    public async Task JobGatheringStarted_EmitsOneSearchStepPerSubQuestion()
    {
        var steps = new FakeStepRepository();
        var outbox = new FakeOutbox();
        var handler = new JobGatheringStartedDomainEventHandler(CreateStepDispatcher(steps, outbox), TestOptions.Of(new JobsOptions()));

        await handler.HandleAsync(new JobGatheringStartedDomainEvent(Guid.NewGuid(), ["Q1", "Q2"], T0), CancellationToken.None);

        steps.Inserted.Select(static s => s.StepKey).ShouldBe([JobStepKeys.Search(0), JobStepKeys.Search(1)]);
        outbox.Enqueued.Select(static e => e.MessageType).ShouldBe([SearchRequested.MessageType, SearchRequested.MessageType]);
    }

    [Fact]
    public async Task JobSynthesisStarted_EmitsSynthesisRequested()
    {
        var steps = new FakeStepRepository();
        var outbox = new FakeOutbox();
        var handler = new JobSynthesisStartedDomainEventHandler(CreateStepDispatcher(steps, outbox), TestOptions.Of(new JobsOptions()));

        await handler.HandleAsync(new JobSynthesisStartedDomainEvent(Guid.NewGuid(), "Question", ["Q1"], T0), CancellationToken.None);

        steps.Inserted.Select(static s => s.StepKey).ShouldBe([JobStepKeys.Synthesize]);
        outbox.Enqueued.Select(static e => e.MessageType).ShouldBe([SynthesisRequested.MessageType]);
    }

    [Fact]
    public async Task TerminalHandlers_EnqueueExpectedOutboxEvents()
    {
        var outbox = new FakeOutbox();
        var envelopes = new FakeEnvelopeFactory();
        var metrics = new JobsMetrics(TestMeters.Create());
        var jobId = Guid.NewGuid();

        await new JobCompletedDomainEventHandler(outbox, envelopes, metrics, NullLogger<JobCompletedDomainEventHandler>.Instance)
            .HandleAsync(new JobCompletedDomainEvent(jobId, JobStatus.Completed, [], T0, T0.AddSeconds(10)), CancellationToken.None);
        await new JobCancelledDomainEventHandler(outbox, envelopes, metrics, NullLogger<JobCancelledDomainEventHandler>.Instance)
            .HandleAsync(new JobCancelledDomainEvent(jobId, T0, T0.AddSeconds(10)), CancellationToken.None);
        await new JobFailedDomainEventHandler(outbox, envelopes, metrics, NullLogger<JobFailedDomainEventHandler>.Instance)
            .HandleAsync(new JobFailedDomainEvent(jobId, JobsErrors.NoSourcesFound.Code, T0, T0.AddSeconds(10)), CancellationToken.None);
        await new JobExpiredDomainEventHandler(outbox, envelopes, metrics, NullLogger<JobExpiredDomainEventHandler>.Instance)
            .HandleAsync(new JobExpiredDomainEvent(jobId, T0, T0.AddSeconds(10)), CancellationToken.None);

        outbox.Enqueued.Select(static e => e.MessageType).ShouldBe([
            JobCompleted.MessageType,
            JobCancelled.MessageType,
            JobFailed.MessageType,
            JobExpired.MessageType,
        ]);
    }

    private static StepDispatcher CreateStepDispatcher(IJobStepRepository steps, IOutbox outbox) => new(
        steps,
        outbox,
        new FakeEnvelopeFactory(),
        TestOptions.Of(new JobsOptions()),
        new FakeTimeProvider(T0));

    private sealed class FakeOutbox : IOutbox
    {
        public List<OutgoingEnvelope> Enqueued { get; } = [];

        public Task EnqueueAsync(OutgoingEnvelope envelope, CancellationToken cancellationToken)
        {
            Enqueued.Add(envelope);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEnvelopeFactory : IEnvelopeFactory
    {
        public OutgoingEnvelope Create<TMessage>(TMessage message)
            where TMessage : IIntegrationMessage =>
            new(Guid.NewGuid(), TMessage.MessageType, "{}", new Dictionary<string, string>(StringComparer.Ordinal), T0);

        public OutgoingEnvelope Recreate(string messageType, string payload, Guid jobId, string stepKey) =>
            new(Guid.NewGuid(), messageType, payload, new Dictionary<string, string>(StringComparer.Ordinal), T0);
    }

    private sealed class FakeStepRepository : IJobStepRepository
    {
        public List<JobStep> Inserted { get; } = [];

        public Task<bool> InsertAsync(JobStep step, CancellationToken cancellationToken)
        {
            Inserted.Add(step);
            return Task.FromResult(true);
        }

        public Task<bool> FinishAsync(Guid jobId, string stepKey, StepStatus status, string? errorCode, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StepCounts> CountAsync(Guid jobId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<JobStep>> ListAsync(Guid jobId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<JobStep>> ListExpiredAsync(Guid jobId, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RenewLeaseAsync(Guid stepId, int attempts, DateTimeOffset leaseUntil, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
