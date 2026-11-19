using Dapper;
using Npgsql;
using Research.Jobs.Domain;
using Research.Jobs.Domain.Ports;
using Research.Providers.Postgres.Transactions;
using Research.SharedKernel.Transactions;

namespace Research.Jobs.Postgres;

/// <summary>Dapper repository over <c>research_jobs</c>. Enums are stored as their names.</summary>
public sealed class ResearchJobRepository(NpgsqlDataSource dataSource) : IResearchJobRepository
{
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

    public async Task<bool> InsertAsync(ResearchJob job, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        var scope = PostgresTransactionScope.From(transaction);
        var inserted = await scope.Connection.ExecuteAsync(new CommandDefinition(
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
            scope.Transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return inserted == 1;
    }

    public async Task<ResearchJob?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<JobRow>(new CommandDefinition(
            $"SELECT {Columns} FROM research_jobs WHERE id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row?.ToDomain();
    }

    public async Task<ResearchJob?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<JobRow>(new CommandDefinition(
            $"SELECT {Columns} FROM research_jobs WHERE idempotency_key = @Key",
            new { Key = idempotencyKey },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row?.ToDomain();
    }

    public Task<ResearchJob?> GetForUpdateAsync(Guid id, ITransactionScope transaction, CancellationToken cancellationToken) =>
        LockAsync(id, "FOR UPDATE", transaction, cancellationToken);

    public Task<ResearchJob?> TryLockAsync(Guid id, ITransactionScope transaction, CancellationToken cancellationToken) =>
        LockAsync(id, "FOR UPDATE SKIP LOCKED", transaction, cancellationToken);

    public async Task<bool> UpdateAsync(
        ResearchJob job,
        int expectedVersion,
        IReadOnlyList<JobStatus> allowedFrom,
        ITransactionScope transaction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(allowedFrom);
        var scope = PostgresTransactionScope.From(transaction);
        var updated = await scope.Connection.ExecuteAsync(new CommandDefinition(
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
                job.Version,
                job.UpdatedAt,
                job.UpdatedBy,
                ExpectedVersion = expectedVersion,
                AllowedFrom = allowedFrom.Select(static s => s.ToString()).ToArray(),
            },
            scope.Transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return updated == 1;
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
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT count(*)::int FROM (SELECT 1 FROM research_jobs WHERE status IN {ActiveStatuses} LIMIT @Limit) active",
            new { Limit = limit },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private async Task<ResearchJob?> LockAsync(Guid id, string lockClause, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        var scope = PostgresTransactionScope.From(transaction);
        var row = await scope.Connection.QuerySingleOrDefaultAsync<JobRow>(new CommandDefinition(
            $"SELECT {Columns} FROM research_jobs WHERE id = @Id {lockClause}",
            new { Id = id },
            scope.Transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row?.ToDomain();
    }

    private async Task<IReadOnlyList<Guid>> ListIdsAsync(string sql, DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
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

        public ResearchJob ToDomain() => new()
        {
            Id = Id,
            Question = Question,
            Status = Enum.Parse<JobStatus>(Status),
            Plan = Plan,
            IdempotencyKey = IdempotencyKey,
            SynthesisCutoffAt = SynthesisCutoffAt,
            DeadlineAt = DeadlineAt,
            TokenBudget = TokenBudget,
            TokensUsed = TokensUsed,
            ErrorCode = ErrorCode,
            Warnings = Warnings,
            GatheringEndReason = GatheringEndReason is null ? null : Enum.Parse<GatheringEndReason>(GatheringEndReason),
            CompletedAt = CompletedAt,
            Version = Version,
            CreatedAt = CreatedAt,
            CreatedBy = CreatedBy,
            UpdatedAt = UpdatedAt,
            UpdatedBy = UpdatedBy,
        };
    }
}
