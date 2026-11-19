using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Research.Messaging;
using Research.Providers.Postgres.Messaging;
using Research.Providers.Postgres.Transactions;
using Research.SharedKernel.Messaging;

namespace Research.IntegrationTests.Postgres;

public sealed class OutboxInboxTests(PostgresFixture postgres)
{
    /// <summary>Retries a failed message on the next pass and never parks it.</summary>
    private static readonly OutboxRetryPolicy RetryAtOnce = new(int.MaxValue, TimeSpan.Zero, TimeSpan.Zero);

    private readonly EnvelopeFactory _envelopes = new(TimeProvider.System);

    [Fact]
    public async Task RolledBackMessage_IsNeverPublished_CommittedOneIsPublishedOnce()
    {
        var transactions = new PostgresTransactionFactory(postgres.Fetching);
        var outbox = new PostgresOutbox();
        var store = new PostgresOutboxStore(postgres.Fetching, NullLogger<PostgresOutboxStore>.Instance);
        await DrainAsync(store);

        var rolledBack = _envelopes.Create(new Research.Contracts.Steps.StepFailed(Guid.NewGuid(), "fetch:a", "x.y", "fetching"));
        await using (var transaction = await transactions.BeginAsync(CancellationToken.None))
        {
            await outbox.EnqueueAsync(rolledBack, transaction, CancellationToken.None);
        }

        var committed = _envelopes.Create(new Research.Contracts.Steps.StepFailed(Guid.NewGuid(), "fetch:b", "x.y", "fetching"));
        await using (var transaction = await transactions.BeginAsync(CancellationToken.None))
        {
            await outbox.EnqueueAsync(committed, transaction, CancellationToken.None);
            await transaction.CommitAsync(CancellationToken.None);
        }

        var published = new List<OutgoingEnvelope>();
        await store.PublishPendingAsync((e, _) => { published.Add(e); return Task.CompletedTask; }, 100, RetryAtOnce, CancellationToken.None);
        await store.PublishPendingAsync((e, _) => { published.Add(e); return Task.CompletedTask; }, 100, RetryAtOnce, CancellationToken.None);

        published.ShouldHaveSingleItem().MessageId.ShouldBe(committed.MessageId);
        published[0].Headers[MessageHeaders.StepKey].ShouldBe("fetch:b");
        // jsonb re-renders whitespace, so compare content, not the exact text.
        published[0].Payload.ShouldContain("fetch:b");
    }

    [Fact]
    public async Task FailedPublish_IsRetriedLater()
    {
        var store = new PostgresOutboxStore(postgres.Fetching, NullLogger<PostgresOutboxStore>.Instance);
        await DrainAsync(store);
        var envelope = await EnqueueAsync("fetch:c");

        await store.PublishPendingAsync((_, _) => throw new InvalidOperationException("broker down"), 100, RetryAtOnce, CancellationToken.None);

        var published = new List<Guid>();
        await store.PublishPendingAsync((e, _) => { published.Add(e.MessageId); return Task.CompletedTask; }, 100, RetryAtOnce, CancellationToken.None);
        published.ShouldBe([envelope.MessageId]);
    }

    [Fact]
    public async Task FailedPublish_WaitsForItsDelay_AndDoesNotHoldBackNewerMessages()
    {
        var store = new PostgresOutboxStore(postgres.Fetching, NullLogger<PostgresOutboxStore>.Instance);
        await DrainAsync(store);
        var failing = await EnqueueAsync("fetch:d");
        var behind = await EnqueueAsync("fetch:e");
        var retryInAnHour = new OutboxRetryPolicy(10, TimeSpan.FromHours(1), TimeSpan.FromHours(1));

        var published = new List<Guid>();
        Task Publish(OutgoingEnvelope envelope, CancellationToken cancellationToken)
        {
            if (envelope.MessageId == failing.MessageId)
            {
                throw new InvalidOperationException("312 NO_ROUTE");
            }

            published.Add(envelope.MessageId);
            return Task.CompletedTask;
        }

        await store.PublishPendingAsync(Publish, 100, retryInAnHour, CancellationToken.None);
        await store.PublishPendingAsync(Publish, 100, retryInAnHour, CancellationToken.None);

        published.ShouldBe([behind.MessageId]);
        var row = await ReadRowAsync(failing.MessageId);
        row.Attempts.ShouldBe(1);
        row.NextAttemptAt.ShouldNotBeNull().ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(59));
        row.ParkedAt.ShouldBeNull();
    }

    [Fact]
    public async Task FailedPublish_IsParked_AfterItsLastAttempt()
    {
        var store = new PostgresOutboxStore(postgres.Fetching, NullLogger<PostgresOutboxStore>.Instance);
        await DrainAsync(store);
        var envelope = await EnqueueAsync("fetch:f");
        var twoAttempts = new OutboxRetryPolicy(2, TimeSpan.Zero, TimeSpan.Zero);

        await store.PublishPendingAsync((_, _) => throw new InvalidOperationException("312 NO_ROUTE"), 100, twoAttempts, CancellationToken.None);
        await store.PublishPendingAsync((_, _) => throw new InvalidOperationException("312 NO_ROUTE"), 100, twoAttempts, CancellationToken.None);

        var published = new List<Guid>();
        await store.PublishPendingAsync((e, _) => { published.Add(e.MessageId); return Task.CompletedTask; }, 100, twoAttempts, CancellationToken.None);

        published.ShouldBeEmpty();
        var row = await ReadRowAsync(envelope.MessageId);
        row.Attempts.ShouldBe(2);
        row.ParkedAt.ShouldNotBeNull();
        row.LastError.ShouldBe("InvalidOperationException: 312 NO_ROUTE");
    }

    [Fact]
    public async Task Inbox_RecordsEachMessageOncePerConsumer()
    {
        var transactions = new PostgresTransactionFactory(postgres.Knowledge);
        var inbox = new PostgresInbox(postgres.Knowledge);
        var messageId = Guid.NewGuid();

        await using (var transaction = await transactions.BeginAsync(CancellationToken.None))
        {
            (await inbox.TryRecordAsync(messageId, "knowledge.a", transaction, CancellationToken.None)).ShouldBeTrue();
            (await inbox.TryRecordAsync(messageId, "knowledge.a", transaction, CancellationToken.None)).ShouldBeFalse();
            (await inbox.TryRecordAsync(messageId, "knowledge.b", transaction, CancellationToken.None)).ShouldBeTrue();
            await transaction.CommitAsync(CancellationToken.None);
        }

        (await inbox.HasProcessedAsync(messageId, "knowledge.a", CancellationToken.None)).ShouldBeTrue();
        (await inbox.HasProcessedAsync(Guid.NewGuid(), "knowledge.a", CancellationToken.None)).ShouldBeFalse();
    }

    private static async Task DrainAsync(PostgresOutboxStore store)
    {
        while (await store.PublishPendingAsync((_, _) => Task.CompletedTask, 1000, RetryAtOnce, CancellationToken.None) > 0)
        {
        }
    }

    private async Task<OutgoingEnvelope> EnqueueAsync(string stepKey)
    {
        var envelope = _envelopes.Create(new Research.Contracts.Steps.StepFailed(Guid.NewGuid(), stepKey, "x.y", "fetching"));
        await using var transaction = await new PostgresTransactionFactory(postgres.Fetching).BeginAsync(CancellationToken.None);
        await new PostgresOutbox().EnqueueAsync(envelope, transaction, CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);
        return envelope;
    }

    private async Task<OutboxRowState> ReadRowAsync(Guid id)
    {
        await using var connection = await postgres.Fetching.OpenConnectionAsync();
        return await connection.QuerySingleAsync<OutboxRowState>(
            "SELECT attempts, last_error, next_attempt_at, parked_at FROM outbox_messages WHERE id = @Id",
            new { Id = id });
    }

    private sealed record OutboxRowState(int Attempts, string? LastError, DateTime? NextAttemptAt, DateTime? ParkedAt);
}
