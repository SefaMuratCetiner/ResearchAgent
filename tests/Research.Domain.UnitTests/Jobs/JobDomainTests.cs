using Research.Jobs.Domain;

namespace Research.Domain.UnitTests.Jobs;

public sealed class JobStateMachineTests
{
    public static TheoryData<JobStatus, JobStatus> Allowed => new()
    {
        { JobStatus.Planning, JobStatus.Gathering },
        { JobStatus.Gathering, JobStatus.Synthesizing },
        { JobStatus.Synthesizing, JobStatus.Completed },
        { JobStatus.Synthesizing, JobStatus.CompletedWithWarnings },
        { JobStatus.Planning, JobStatus.Failed },
        { JobStatus.Gathering, JobStatus.Cancelled },
        { JobStatus.Synthesizing, JobStatus.Expired },
    };

    public static TheoryData<JobStatus, JobStatus> Forbidden => new()
    {
        { JobStatus.Planning, JobStatus.Synthesizing },
        { JobStatus.Gathering, JobStatus.Completed },
        { JobStatus.Completed, JobStatus.Failed },
        { JobStatus.Failed, JobStatus.Gathering },
        { JobStatus.Cancelled, JobStatus.Expired },
        { JobStatus.Expired, JobStatus.Cancelled },
    };

    [Theory]
    [MemberData(nameof(Allowed))]
    public void CanMove_AllowedTransition_IsTrue(JobStatus from, JobStatus to) =>
        JobStateMachine.CanMove(from, to).ShouldBeTrue();

    [Theory]
    [MemberData(nameof(Forbidden))]
    public void CanMove_ForbiddenTransition_IsFalse(JobStatus from, JobStatus to) =>
        JobStateMachine.CanMove(from, to).ShouldBeFalse();

    [Fact]
    public void TerminalStates_AreAbsorbing()
    {
        var terminal = Enum.GetValues<JobStatus>().Where(static s => s.IsTerminal()).ToList();
        terminal.ShouldBe([JobStatus.Completed, JobStatus.CompletedWithWarnings, JobStatus.Failed, JobStatus.Cancelled, JobStatus.Expired]);

        foreach (var from in terminal)
        {
            foreach (var to in Enum.GetValues<JobStatus>())
            {
                JobStateMachine.CanMove(from, to).ShouldBeFalse($"{from} -> {to}");
            }
        }
    }

    [Fact]
    public void HasReport_OnlyForCompletedStates()
    {
        Enum.GetValues<JobStatus>().Where(static s => s.HasReport())
            .ShouldBe([JobStatus.Completed, JobStatus.CompletedWithWarnings]);
    }
}

public sealed class JobOutcomePolicyTests
{
    [Theory]
    [InlineData(0, 3, "jobs.no_sources_found")]
    [InlineData(2, 3, "jobs.insufficient_sources")]
    [InlineData(3, 3, null)]
    [InlineData(7, 3, null)]
    public void EvaluateSources_ReturnsExpectedError(int indexed, int min, string? expectedCode) =>
        (JobOutcomePolicy.EvaluateSources(indexed, min)?.Code).ShouldBe(expectedCode);

    [Fact]
    public void Complete_WithoutIssues_IsCompleted()
    {
        var (status, warnings) = JobOutcomePolicy.Complete(GatheringEndReason.AllStepsDone, failedSteps: 0, reportTruncated: false);
        status.ShouldBe(JobStatus.Completed);
        warnings.ShouldBeEmpty();
    }

    [Fact]
    public void Complete_CollectsEveryWarning()
    {
        var (status, warnings) = JobOutcomePolicy.Complete(GatheringEndReason.CutoffReached, failedSteps: 2, reportTruncated: true);
        status.ShouldBe(JobStatus.CompletedWithWarnings);
        warnings.ShouldBe(["jobs.cutoff_reached", "jobs.steps_failed", "synthesis.output_truncated"]);
    }

    [Fact]
    public void Complete_BudgetExhausted_IsAWarning()
    {
        var (status, warnings) = JobOutcomePolicy.Complete(GatheringEndReason.BudgetExhausted, failedSteps: 0, reportTruncated: false);
        status.ShouldBe(JobStatus.CompletedWithWarnings);
        warnings.ShouldBe(["jobs.budget_exhausted"]);
    }
}

public sealed class JobStepKeysTests
{
    [Fact]
    public void Fetch_IsDeterministicPerUrl()
    {
        JobStepKeys.Fetch("https://example.com/a").ShouldBe(JobStepKeys.Fetch("https://example.com/a"));
        JobStepKeys.Fetch("https://example.com/a").ShouldNotBe(JobStepKeys.Fetch("https://example.com/b"));
        JobStepKeys.Fetch("https://example.com/a").ShouldMatch("^fetch:[0-9a-f]{24}$");
    }

    [Theory]
    [InlineData("plan", StepKind.Plan)]
    [InlineData("synthesize", StepKind.Synthesize)]
    [InlineData("search:0", StepKind.Search)]
    [InlineData("search:12", StepKind.Search)]
    [InlineData("fetch:0123456789abcdef01234567", StepKind.Fetch)]
    public void KindOf_ReadsTheKey(string key, StepKind kind) => JobStepKeys.KindOf(key).ShouldBe(kind);

    [Fact]
    public void Search_UsesInvariantIndex() => JobStepKeys.Search(3).ShouldBe("search:3");
}
