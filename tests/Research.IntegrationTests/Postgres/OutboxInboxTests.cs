using Microsoft.Extensions.Logging.Abstractions;
using Research.Messaging;
using Research.Providers.Postgres.Messaging;
using Research.Providers.Postgres.Transactions;
using Research.SharedKernel.Messaging;

namespace Research.IntegrationTests.Postgres;

public sealed class OutboxInboxTests(PostgresFixture postgres)
{
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
        await store.PublishPendingAsync((e, _) => { published.Add(e); return Task.CompletedTask; }, 100, CancellationToken.None);
        await store.PublishPendingAsync((e, _) => { published.Add(e); return Task.CompletedTask; }, 100, CancellationToken.None);

        published.ShouldHaveSingleItem().MessageId.ShouldBe(committed.MessageId);
        published[0].Headers[MessageHeaders.StepKey].ShouldBe("fetch:b");
        // jsonb re-renders whitespace, so compare content, not the exact text.
        published[0].Payload.ShouldContain("fetch:b");
    }

    [Fact]
    public async Task FailedPublish_IsRetriedLater()
    {
        var transactions = new PostgresTransactionFactory(postgres.Fetching);
        var store = new PostgresOutboxStore(postgres.Fetching, NullLogger<PostgresOutboxStore>.Instance);
        await DrainAsync(store);

        var envelope = _envelopes.Create(new Research.Contracts.Steps.StepFailed(Guid.NewGuid(), "fetch:c", "x.y", "fetching"));
        await using (var transaction = await transactions.BeginAsync(CancellationToken.None))
        {
            await new PostgresOutbox().EnqueueAsync(envelope, transaction, CancellationToken.None);
            await transaction.CommitAsync(CancellationToken.None);
        }

        await store.PublishPendingAsync((_, _) => throw new InvalidOperationException("broker down"), 100, CancellationToken.None);

        var published = new List<Guid>();
        await store.PublishPendingAsync((e, _) => { published.Add(e.MessageId); return Task.CompletedTask; }, 100, CancellationToken.None);
        published.ShouldBe([envelope.MessageId]);
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
        while (await store.PublishPendingAsync((_, _) => Task.CompletedTask, 1000, CancellationToken.None) > 0)
        {
        }
    }
}
