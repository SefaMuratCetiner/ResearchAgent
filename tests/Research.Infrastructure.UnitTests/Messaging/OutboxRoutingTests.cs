using Research.Contracts;
using Research.Contracts.Jobs;
using Research.Contracts.Planning;
using Research.Shared.Adapters.Messaging;
using Research.Shared.Messaging;

namespace Research.Infrastructure.UnitTests.Messaging;

/// <summary>Which messages must reach a consumer (PLAN §11), and how the outbox retries the ones that fail.</summary>
public sealed class OutboxRoutingTests
{
    public static TheoryData<string, MessageKind> Kinds => new()
    {
        { "planning.plan-requested", MessageKind.Command },
        { "search.search-requested", MessageKind.Command },
        { "fetching.fetch-requested", MessageKind.Command },
        { "synthesis.synthesis-requested", MessageKind.Command },
        { "jobs.job-created", MessageKind.Event },
        { "jobs.job-completed", MessageKind.Event },
        { "jobs.job-failed", MessageKind.Event },
        { "jobs.job-cancelled", MessageKind.Event },
        { "jobs.job-expired", MessageKind.Event },
        { "planning.plan-ready", MessageKind.Event },
        { "search.results-found", MessageKind.Event },
        { "fetching.document-fetched", MessageKind.Event },
        { "knowledge.document-indexed", MessageKind.Event },
        { "synthesis.report-ready", MessageKind.Event },
        { "research.step-failed", MessageKind.Event },
    };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void KindOf_FollowsThePlan(string messageType, MessageKind kind) =>
        MessageCatalog.KindOf(messageType).ShouldBe(kind);

    [Fact]
    public void Catalog_HasEveryContract_AndTheTableAbove() =>
        MessageCatalog.MessageTypes.Order(StringComparer.Ordinal)
            .ShouldBe(Kinds.Select(static row => row.Data.Item1).Order(StringComparer.Ordinal));

    [Fact]
    public void KindOf_ReadsTheContract() =>
        (MessageCatalog.KindOf(JobCreated.MessageType), MessageCatalog.KindOf(PlanRequested.MessageType))
            .ShouldBe((JobCreated.Kind, PlanRequested.Kind));

    [Fact]
    public void KindOf_UnknownType_IsACommand_SoItIsNeverDroppedSilently() =>
        MessageCatalog.KindOf("nobody.knows-this").ShouldBe(MessageKind.Command);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(5, 16)]
    [InlineData(9, 256)]
    [InlineData(10, 300)]
    [InlineData(1000, 300)]
    public void DelayAfter_DoublesUpToTheMaximum(int failedAttempts, int seconds) =>
        new OutboxRetryPolicy(20, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(300))
            .DelayAfter(failedAttempts).ShouldBe(TimeSpan.FromSeconds(seconds));

    [Fact]
    public void ShouldPark_OnlyAtTheLastAttempt()
    {
        var policy = new OutboxRetryPolicy(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(300));

        policy.ShouldPark(2).ShouldBeFalse();
        policy.ShouldPark(3).ShouldBeTrue();
    }

    [Fact]
    public void DefaultSettings_ParkAfterAboutAnHour()
    {
        var options = new MessagingOptions();
        var policy = new OutboxRetryPolicy(
            options.OutboxMaxAttempts,
            TimeSpan.FromMilliseconds(options.OutboxRetryDelayMs),
            TimeSpan.FromSeconds(options.OutboxMaxRetryDelaySeconds));

        var total = Enumerable.Range(1, options.OutboxMaxAttempts - 1).Aggregate(TimeSpan.Zero, (sum, n) => sum + policy.DelayAfter(n));

        total.ShouldBeInRange(TimeSpan.FromMinutes(45), TimeSpan.FromMinutes(75));
    }

    [Theory]
    [InlineData(0, 300, "Messaging:OutboxMaxAttempts must be at least 1.")]
    [InlineData(20, 0, "Messaging:OutboxMaxRetryDelaySeconds must be positive.")]
    public void Validator_RejectsBadRetrySettings(int maxAttempts, int maxDelaySeconds, string failure)
    {
        var options = new MessagingOptions
        {
            ServiceName = "test",
            OutboxMaxAttempts = maxAttempts,
            OutboxMaxRetryDelaySeconds = maxDelaySeconds,
        };

        new MessagingOptionsValidator().Validate(null, options).Failures.ShouldBe([failure]);
    }
}
