using Dapper;
using Npgsql;
using Research.Jobs.Domain;
using Research.Jobs.Domain.Ports;
using Research.Providers.Postgres.Transactions;
using Research.SharedKernel.Transactions;

namespace Research.Jobs.Postgres;

/// <summary>Dapper repository over <c>job_steps</c>.</summary>
public sealed class JobStepRepository(NpgsqlDataSource dataSource) : IJobStepRepository
{
    private const string Columns = """
        id, job_id, kind, step_key, status, attempts, lease_until, last_error_code, trace_id, request_type,
        request_payload::text AS request_payload, created_at, created_by, updated_at, updated_by
        """;

    private const string InsertSql = """
        INSERT INTO job_steps (id, job_id, kind, step_key, status, attempts, lease_until, trace_id, request_type,
            request_payload, created_at, created_by)
        VALUES (@Id, @JobId, @Kind, @StepKey, @Status, @Attempts, @LeaseUntil, @TraceId, @RequestType,
            @RequestPayload::jsonb, @CreatedAt, @CreatedBy)
        ON CONFLICT (job_id, step_key) DO NOTHING
        """;

    private const string FinishSql = """
        UPDATE job_steps
        SET status = @Status, last_error_code = @ErrorCode, updated_at = @Now, updated_by = @Actor
        WHERE job_id = @JobId AND step_key = @StepKey AND status = 'Running'
        """;

    private const string CountSql = """
        SELECT
            count(*) FILTER (WHERE kind IN ('Search', 'Fetch') AND status = 'Running')   AS gathering_running,
            count(*) FILTER (WHERE kind = 'Fetch' AND status = 'Succeeded')             AS fetch_succeeded,
            count(*) FILTER (WHERE kind IN ('Search', 'Fetch') AND status = 'Failed')    AS gathering_failed,
            count(*) FILTER (WHERE kind = 'Fetch')                                       AS fetch_total
        FROM job_steps WHERE job_id = @JobId
        """;

    private const string RenewSql = """
        UPDATE job_steps SET attempts = @Attempts, lease_until = @LeaseUntil, updated_at = @Now, updated_by = @Actor
        WHERE id = @Id
        """;

    public async Task<bool> InsertAsync(JobStep step, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);
        var scope = PostgresTransactionScope.From(transaction);
        var inserted = await scope.Connection.ExecuteAsync(new CommandDefinition(
            InsertSql,
            new
            {
                step.Id,
                step.JobId,
                Kind = step.Kind.ToString(),
                step.StepKey,
                Status = step.Status.ToString(),
                step.Attempts,
                step.LeaseUntil,
                step.TraceId,
                step.RequestType,
                step.RequestPayload,
                step.CreatedAt,
                step.CreatedBy,
            },
            scope.Transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return inserted == 1;
    }

    public async Task<bool> FinishAsync(
        Guid jobId,
        string stepKey,
        StepStatus status,
        string? errorCode,
        DateTimeOffset now,
        string actor,
        ITransactionScope transaction,
        CancellationToken cancellationToken)
    {
        var scope = PostgresTransactionScope.From(transaction);
        var updated = await scope.Connection.ExecuteAsync(new CommandDefinition(
            FinishSql,
            new { JobId = jobId, StepKey = stepKey, Status = status.ToString(), ErrorCode = errorCode, Now = now, Actor = actor },
            scope.Transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return updated == 1;
    }

    public async Task<StepCounts> CountAsync(Guid jobId, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        var scope = PostgresTransactionScope.From(transaction);
        var row = await scope.Connection.QuerySingleAsync<CountRow>(new CommandDefinition(
            CountSql,
            new { JobId = jobId },
            scope.Transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return new StepCounts((int)row.GatheringRunning, (int)row.FetchSucceeded, (int)row.GatheringFailed, (int)row.FetchTotal);
    }

    public async Task<IReadOnlyList<JobStep>> ListAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<StepRow>(new CommandDefinition(
            $"SELECT {Columns} FROM job_steps WHERE job_id = @JobId ORDER BY created_at, id",
            new { JobId = jobId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(static r => r.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<JobStep>> ListExpiredAsync(Guid jobId, DateTimeOffset now, ITransactionScope transaction, CancellationToken cancellationToken)
    {
        var scope = PostgresTransactionScope.From(transaction);
        var rows = await scope.Connection.QueryAsync<StepRow>(new CommandDefinition(
            $"SELECT {Columns} FROM job_steps WHERE job_id = @JobId AND status = 'Running' AND lease_until <= @Now ORDER BY created_at, id",
            new { JobId = jobId, Now = now },
            scope.Transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(static r => r.ToDomain()).ToList();
    }

    public async Task RenewLeaseAsync(
        Guid stepId,
        int attempts,
        DateTimeOffset leaseUntil,
        DateTimeOffset now,
        string actor,
        ITransactionScope transaction,
        CancellationToken cancellationToken)
    {
        var scope = PostgresTransactionScope.From(transaction);
        await scope.Connection.ExecuteAsync(new CommandDefinition(
            RenewSql,
            new { Id = stepId, Attempts = attempts, LeaseUntil = leaseUntil, Now = now, Actor = actor },
            scope.Transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private sealed class CountRow
    {
        public long GatheringRunning { get; set; }

        public long FetchSucceeded { get; set; }

        public long GatheringFailed { get; set; }

        public long FetchTotal { get; set; }
    }

    private sealed class StepRow
    {
        public Guid Id { get; set; }

        public Guid JobId { get; set; }

        public string Kind { get; set; } = string.Empty;

        public string StepKey { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public int Attempts { get; set; }

        public DateTimeOffset LeaseUntil { get; set; }

        public string? LastErrorCode { get; set; }

        public string? TraceId { get; set; }

        public string RequestType { get; set; } = string.Empty;

        public string RequestPayload { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        public string CreatedBy { get; set; } = string.Empty;

        public DateTimeOffset? UpdatedAt { get; set; }

        public string? UpdatedBy { get; set; }

        public JobStep ToDomain() => new()
        {
            Id = Id,
            JobId = JobId,
            Kind = Enum.Parse<StepKind>(Kind),
            StepKey = StepKey,
            Status = Enum.Parse<StepStatus>(Status),
            Attempts = Attempts,
            LeaseUntil = LeaseUntil,
            LastErrorCode = LastErrorCode,
            TraceId = TraceId,
            RequestType = RequestType,
            RequestPayload = RequestPayload,
            CreatedAt = CreatedAt,
            CreatedBy = CreatedBy,
            UpdatedAt = UpdatedAt,
            UpdatedBy = UpdatedBy,
        };
    }
}
