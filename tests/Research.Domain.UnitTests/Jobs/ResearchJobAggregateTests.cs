using Research.Jobs.Domain;
using Research.Jobs.Domain.Events;

namespace Research.Domain.UnitTests.Jobs;

public sealed class ResearchJobAggregateTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_StartsInPlanningWithClocksAndRaisesCreatedEvent()
    {
        var id = Guid.NewGuid();
        var job = ResearchJob.Create(
            id,
            "What is pgvector?",
            "idem-1",
            maxDuration: TimeSpan.FromMinutes(5),
            synthesisReserve: TimeSpan.FromSeconds(90),
            tokenBudget: 10_000,
            T0,
            "api");

        job.Id.ShouldBe(id);
        job.Status.ShouldBe(JobStatus.Planning);
        job.SynthesisCutoffAt.ShouldBe(T0 + TimeSpan.FromSeconds(210));
        job.DeadlineAt.ShouldBe(T0 + TimeSpan.FromMinutes(5));
        job.CreatedAt.ShouldBe(T0);
        job.CreatedBy.ShouldBe("api");
        var created = job.DomainEvents.OfType<ResearchJobCreatedDomainEvent>().ShouldHaveSingleItem();
        created.JobId.ShouldBe(id);
        created.Question.ShouldBe("What is pgvector?");
    }

    [Fact]
    public void RecordTokens_IgnoresNonPositive_AndTracksBudgetExhausted()
    {
        var job = CreateSampleJob(tokenBudget: 100);

        job.RecordTokens(0).ShouldBeFalse();
        job.RecordTokens(-5).ShouldBeFalse();
        job.TokensUsed.ShouldBe(0);
        job.BudgetExhausted.ShouldBeFalse();

        job.RecordTokens(60).ShouldBeTrue();
        job.BudgetExhausted.ShouldBeFalse();

        job.RecordTokens(40).ShouldBeTrue();
        job.TokensUsed.ShouldBe(100);
        job.BudgetExhausted.ShouldBeTrue();
    }

    [Fact]
    public void StartGathering_ValidPlan_MovesToGatheringAndRaisesEvent()
    {
        var job = CreateSampleJob();
        job.ClearDomainEvents();

        var started = job.StartGathering(["Q1", "Q2"], T0.AddSeconds(5));

        started.ShouldBeTrue();
        job.Status.ShouldBe(JobStatus.Gathering);
        job.Plan.ShouldBe(["Q1", "Q2"]);
        var startedEvent = job.DomainEvents.OfType<GatheringStartedDomainEvent>().ShouldHaveSingleItem();
        startedEvent.SubQuestions.Count.ShouldBe(2);
    }

    [Fact]
    public void StartGathering_EmptyPlan_FailsWithPlanEmpty()
    {
        var job = CreateSampleJob();
        job.ClearDomainEvents();

        var started = job.StartGathering([], T0.AddSeconds(5));

        started.ShouldBeFalse();
        job.Status.ShouldBe(JobStatus.Failed);
        job.ErrorCode.ShouldBe(JobsErrors.PlanEmpty.Code);
        var failed = job.DomainEvents.OfType<FailedDomainEvent>().ShouldHaveSingleItem();
        failed.ErrorCode.ShouldBe(JobsErrors.PlanEmpty.Code);
    }

    [Fact]
    public void EndGathering_WithEnoughSources_MovesToSynthesizing()
    {
        var job = CreateSampleJob();
        job.StartGathering(["Q1"], T0.AddSeconds(5));
        job.ClearDomainEvents();

        var changed = job.EndGathering(GatheringEndReason.AllStepsDone, indexedSources: 4, minSources: 3, T0.AddSeconds(20));

        changed.ShouldBeTrue();
        job.Status.ShouldBe(JobStatus.Synthesizing);
        job.GatheringEndReason.ShouldBe(GatheringEndReason.AllStepsDone);
        job.DomainEvents.ShouldContain(e => e is SynthesisStartedDomainEvent);
    }

    [Fact]
    public void EndGathering_WithInsufficientSources_FailsJob()
    {
        var job = CreateSampleJob();
        job.StartGathering(["Q1"], T0.AddSeconds(5));
        job.ClearDomainEvents();

        var changed = job.EndGathering(GatheringEndReason.CutoffReached, indexedSources: 1, minSources: 3, T0.AddSeconds(20));

        changed.ShouldBeTrue();
        job.Status.ShouldBe(JobStatus.Failed);
        job.ErrorCode.ShouldBe(JobsErrors.InsufficientSources.Code);
        var failed = job.DomainEvents.OfType<FailedDomainEvent>().ShouldHaveSingleItem();
        failed.ErrorCode.ShouldBe(JobsErrors.InsufficientSources.Code);
    }

    [Fact]
    public void Cancel_ActiveJob_CancelsAndIsIdempotent()
    {
        var job = CreateSampleJob();
        job.ClearDomainEvents();

        var first = job.Cancel(T0.AddSeconds(10));
        first.IsSuccess.ShouldBeTrue();
        job.Status.ShouldBe(JobStatus.Cancelled);
        job.DomainEvents.Count.ShouldBe(1);
        job.DomainEvents.Single().ShouldBeOfType<CancelledDomainEvent>();

        var second = job.Cancel(T0.AddSeconds(15));
        second.IsSuccess.ShouldBeTrue();
        job.DomainEvents.Count.ShouldBe(1);
    }

    [Fact]
    public void Cancel_CompletedJob_ReturnsJobAlreadyCompleted()
    {
        var job = CreateSampleJob();
        job.StartGathering(["Q1"], T0.AddSeconds(5));
        job.EndGathering(GatheringEndReason.AllStepsDone, 3, 3, T0.AddSeconds(10));
        job.Complete(failedSteps: 0, reportTruncated: false, T0.AddSeconds(20));

        var result = job.Cancel(T0.AddSeconds(25));

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(JobsErrors.JobAlreadyCompleted);
    }

    [Fact]
    public void Cancel_FailedOrExpiredJob_ReturnsJobNotCancellable()
    {
        var job = CreateSampleJob();
        job.Expire(T0.AddMinutes(6));

        var result = job.Cancel(T0.AddMinutes(7));

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBe(JobsErrors.JobNotCancellable);
    }

    [Fact]
    public void Complete_WithCutoffAndTruncation_CompletesWithWarnings()
    {
        var job = CreateSampleJob();
        job.StartGathering(["Q1"], T0.AddSeconds(5));
        job.EndGathering(GatheringEndReason.CutoffReached, 3, 3, T0.AddSeconds(10));
        job.ClearDomainEvents();

        var completed = job.Complete(failedSteps: 1, reportTruncated: true, T0.AddSeconds(20));

        completed.ShouldBeTrue();
        job.Status.ShouldBe(JobStatus.CompletedWithWarnings);
        job.Warnings.ShouldBe([JobsErrors.CutoffReached.Code, JobsErrors.StepsFailed.Code, JobsErrors.OutputTruncatedWarning]);
        var completedEvent = job.DomainEvents.OfType<CompletedDomainEvent>().ShouldHaveSingleItem();
        completedEvent.Status.ShouldBe(JobStatus.CompletedWithWarnings);
    }

    [Fact]
    public void MarkPersisted_UpdatesVersionAndAuditFields()
    {
        var job = CreateSampleJob();
        job.StartGathering(["Q1"], T0.AddSeconds(5));

        job.Version.ShouldBe(0);

        job.MarkPersisted(1, T0.AddSeconds(6), "system:jobs");
        job.Version.ShouldBe(1);
        job.UpdatedAt.ShouldBe(T0.AddSeconds(6));
        job.UpdatedBy.ShouldBe("system:jobs");
    }

    private static ResearchJob CreateSampleJob(long tokenBudget = 10_000) => ResearchJob.Create(
        Guid.NewGuid(),
        "Sample research question?",
        idempotencyKey: null,
        maxDuration: TimeSpan.FromMinutes(5),
        synthesisReserve: TimeSpan.FromSeconds(90),
        tokenBudget: tokenBudget,
        T0,
        "api");
}
