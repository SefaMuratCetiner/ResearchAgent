using System.Runtime.InteropServices;
using Dapper;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Domain;
using Research.Providers.Postgres.Transactions;

namespace Research.Jobs.Infrastructure.Persistence;

/// <summary>Dapper repository over <c>research_jobs</c>. Tracks persisted CAS snapshots in scope; enums are stored as their names.</summary>
public sealed class ResearchJobRepository(PostgresUnitOfWork unitOfWork) : IResearchJobRepository
{
    private readonly Dictionary<Guid, PersistedSnapshot> _snapshots = [];
    private const string Columns = """
        id, question, status, plan, idempotency_key, synthesis_cutoff_at, deadline_at, token_budget, tokens_used,
        error_code, warnings, gathering_end_reason, completed_at, version, created_at, created_by, updated_at, updated_by
        """;

    private const string InsertSql = """
        INSERT INTO research_jobs (id, question, status, plan, idempotency_key, synthesis_cutoff_at, deadline_at,
            token_budget, tokens_used, warnings, version, created_at, created_by)
        VALUES (@Id, @Question, @Status, @Plan, @IdempotencyKey, @SynthesisCutoffAt, @DeadlineAt,
            @TokenBudget, @TokensUsed, @Warnings, @Version, @CreatedAt, @CreatedBy)
        ON CONFLICT DO NOTHING
        """;

    private const string UpdateSql = """
        UPDATE research_jobs
        SET status = @Status, plan = @Plan, tokens_used = @TokensUsed, error_code = @ErrorCode, warnings = @Warnings,
            gathering_end_reason = @GatheringEndReason, completed_at = @CompletedAt, version = @Version,
            updated_at = @UpdatedAt, updated_by = @UpdatedBy
        WHERE id = @Id AND version = @ExpectedVersion AND status = ANY(@AllowedFrom)
        """;

    private const string ActiveStatuses = "('Planning', 'Gathering', 'Synthesizing')";

    public async Task<bool> InsertAsync(ResearchJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.CreatedAt == default || string.IsNullOrWhiteSpace(job.CreatedBy))
        {
            job.StampCreated(
                job.CreatedAt == default ? unitOfWork.Now : job.CreatedAt,
                string.IsNullOrWhiteSpace(job.CreatedBy) ? unitOfWork.ActorId : job.CreatedBy);
        }

        var inserted = await unitOfWork.ActiveConnection.ExecuteAsync(new CommandDefinition(
            InsertSql,
            new
            {
                job.Id,
                job.Question,
                Status = job.Status.ToString(),
                Plan = job.Plan.ToArray(),
                job.IdempotencyKey,
                job.SynthesisCutoffAt,
                job.DeadlineAt,
                job.TokenBudget,
                job.TokensUsed,
                Warnings = job.Warnings.ToArray(),
                job.Version,
                job.CreatedAt,
                job.CreatedBy,
            },
            unitOfWork.ActiveTransaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (inserted == 1)
        {
            RecordSnapshot(job);
            unitOfWork.Track(job);
            return true;
        }

        return false;
    }

    public async Task<ResearchJob?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await unitOfWork.DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<JobRow>(new CommandDefinition(
            $"SELECT {Columns} FROM research_jobs WHERE id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return RehydrateAndTrack(row);
    }

    public async Task<ResearchJob?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        await using var connection = await unitOfWork.DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<JobRow>(new CommandDefinition(
            $"SELECT {Columns} FROM research_jobs WHERE idempotency_key = @Key",
            new { Key = idempotencyKey },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return RehydrateAndTrack(row);
    }

    public Task<ResearchJob?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        LockAsync(id, "FOR UPDATE", cancellationToken);

    public Task<ResearchJob?> TryLockAsync(Guid id, CancellationToken cancellationToken) =>
        LockAsync(id, "FOR UPDATE SKIP LOCKED", cancellationToken);

    public async Task<bool> UpdateAsync(ResearchJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        var now = unitOfWork.Now;
        var actor = unitOfWork.ActorId;
        var snapshot = _snapshots.TryGetValue(job.Id, out var loaded)
            ? loaded
            : new PersistedSnapshot(job.Version, job.Status);
        var expectedVersion = snapshot.Version;
        var nextVersion = expectedVersion + 1;
        var allowedFrom = ResolveAllowedSourceStatuses(snapshot.Status, job.Status);

        var updated = await unitOfWork.ActiveConnection.ExecuteAsync(new CommandDefinition(
            UpdateSql,
            new
            {
                job.Id,
                Status = job.Status.ToString(),
                Plan = job.Plan.ToArray(),
                job.TokensUsed,
                job.ErrorCode,
                Warnings = job.Warnings.ToArray(),
                GatheringEndReason = job.GatheringEndReason?.ToString(),
                job.CompletedAt,
                Version = nextVersion,
                UpdatedAt = now,
                UpdatedBy = actor,
                ExpectedVersion = expectedVersion,
                AllowedFrom = allowedFrom.Select(static s => s.ToString()).ToArray(),
            },
            unitOfWork.ActiveTransaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (updated == 1)
        {
            job.MarkPersisted(nextVersion, now, actor);
            RecordSnapshot(job);
            unitOfWork.Track(job);
            return true;
        }

        return false;
    }

    public Task<IReadOnlyList<Guid>> ListPastCutoffAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
        ListIdsAsync(
            "SELECT id FROM research_jobs WHERE status = 'Gathering' AND synthesis_cutoff_at <= @Now ORDER BY synthesis_cutoff_at LIMIT @Limit",
            now,
            limit,
            cancellationToken);

    public Task<IReadOnlyList<Guid>> ListPastDeadlineAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
        ListIdsAsync(
            $"SELECT id FROM research_jobs WHERE status IN {ActiveStatuses} AND deadline_at <= @Now ORDER BY deadline_at LIMIT @Limit",
            now,
            limit,
            cancellationToken);

    public Task<IReadOnlyList<Guid>> ListWithExpiredStepsAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) =>
        ListIdsAsync(
            $"""
            SELECT DISTINCT s.job_id FROM job_steps s
            JOIN research_jobs j ON j.id = s.job_id
            WHERE s.status = 'Running' AND s.lease_until <= @Now AND j.status IN {ActiveStatuses}
            LIMIT @Limit
            """,
            now,
            limit,
            cancellationToken);

    public async Task<int> CountActiveAsync(int limit, CancellationToken cancellationToken)
    {
        // Counting a LIMITed subquery stops early, so the check stays cheap however many jobs are active.
        await using var connection = await unitOfWork.DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT count(*)::int FROM (SELECT 1 FROM research_jobs WHERE status IN {ActiveStatuses} LIMIT @Limit) active",
            new { Limit = limit },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    internal static IReadOnlyList<JobStatus> ResolveAllowedSourceStatuses(JobStatus persistedStatus, JobStatus currentStatus) =>
        currentStatus == persistedStatus ? [persistedStatus] : JobStateMachine.AllowedFrom(currentStatus);

    private ResearchJob? RehydrateAndTrack(JobRow? row)
    {
        if (row is null)
        {
            return null;
        }

        var job = row.ToDomain();
        RecordSnapshot(job);
        return job;
    }

    private void RecordSnapshot(ResearchJob job) =>
        _snapshots[job.Id] = new PersistedSnapshot(job.Version, job.Status);

    private async Task<ResearchJob?> LockAsync(Guid id, string lockClause, CancellationToken cancellationToken)
    {
        var row = await unitOfWork.ActiveConnection.QuerySingleOrDefaultAsync<JobRow>(new CommandDefinition(
            $"SELECT {Columns} FROM research_jobs WHERE id = @Id {lockClause}",
            new { Id = id },
            unitOfWork.ActiveTransaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return RehydrateAndTrack(row);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct PersistedSnapshot(int Version, JobStatus Status);

    private async Task<IReadOnlyList<Guid>> ListIdsAsync(string sql, DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        await using var connection = await unitOfWork.DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var ids = await connection.QueryAsync<Guid>(new CommandDefinition(
            sql,
            new { Now = now, Limit = limit },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return ids.AsList();
    }

    private sealed class JobRow
    {
        public Guid Id { get; set; }

        public string Question { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string[] Plan { get; set; } = [];

        public string? IdempotencyKey { get; set; }

        public DateTimeOffset SynthesisCutoffAt { get; set; }

        public DateTimeOffset DeadlineAt { get; set; }

        public long TokenBudget { get; set; }

        public long TokensUsed { get; set; }

        public string? ErrorCode { get; set; }

        public string[] Warnings { get; set; } = [];

        public string? GatheringEndReason { get; set; }

        public DateTimeOffset? CompletedAt { get; set; }

        public int Version { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public string CreatedBy { get; set; } = string.Empty;

        public DateTimeOffset? UpdatedAt { get; set; }

        public string? UpdatedBy { get; set; }

        public ResearchJob ToDomain() => ResearchJob.Rehydrate(
            Id,
            Question,
            Enum.Parse<JobStatus>(Status),
            Plan,
            IdempotencyKey,
            SynthesisCutoffAt,
            DeadlineAt,
            TokenBudget,
            TokensUsed,
            ErrorCode,
            Warnings,
            GatheringEndReason is null ? null : Enum.Parse<GatheringEndReason>(GatheringEndReason),
            CompletedAt,
            Version,
            CreatedAt,
            CreatedBy,
            UpdatedAt,
            UpdatedBy);
    }
}
