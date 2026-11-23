using System.Text.Json;
using Dapper;
using Research.Jobs.Application.Common.Interfaces;
using Research.Jobs.Domain;
using Research.Providers.Postgres.Transactions;

namespace Research.Jobs.Infrastructure.Persistence;

/// <summary>Dapper repository over <c>research_reports</c>; citations are stored as jsonb.</summary>
public sealed class ResearchReportRepository(PostgresUnitOfWork unitOfWork) : IResearchReportRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string InsertSql = """
        INSERT INTO research_reports (job_id, content, citations, model, truncated, created_at, created_by)
        VALUES (@JobId, @Content, @Citations::jsonb, @Model, @Truncated, @CreatedAt, @CreatedBy)
        ON CONFLICT (job_id) DO NOTHING
        """;

    private const string SelectSql = """
        SELECT job_id, content, citations::text AS citations, model, truncated, created_at, created_by, updated_at, updated_by
        FROM research_reports WHERE job_id = @JobId
        """;

    public async Task InsertAsync(ResearchReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.CreatedAt == default || string.IsNullOrWhiteSpace(report.CreatedBy))
        {
            report.StampCreated(
                report.CreatedAt == default ? unitOfWork.Now : report.CreatedAt,
                string.IsNullOrWhiteSpace(report.CreatedBy) ? unitOfWork.ActorId : report.CreatedBy);
        }

        await unitOfWork.ActiveConnection.ExecuteAsync(new CommandDefinition(
            InsertSql,
            new
            {
                JobId = report.Id,
                report.Content,
                Citations = JsonSerializer.Serialize(report.Citations, Json),
                report.Model,
                report.Truncated,
                report.CreatedAt,
                report.CreatedBy,
            },
            unitOfWork.ActiveTransaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<ResearchReport?> GetAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var db = await unitOfWork.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.Connection.QuerySingleOrDefaultAsync<ReportRow>(new CommandDefinition(
            SelectSql,
            new { JobId = jobId },
            db.Transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row?.ToDomain();
    }

    private sealed class ReportRow
    {
        public Guid JobId { get; set; }

        public string Content { get; set; } = string.Empty;

        public string Citations { get; set; } = "[]";

        public string Model { get; set; } = string.Empty;

        public bool Truncated { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public string CreatedBy { get; set; } = string.Empty;

        public DateTimeOffset? UpdatedAt { get; set; }

        public string? UpdatedBy { get; set; }

        public ResearchReport ToDomain()
        {
            var report = new ResearchReport
            {
                Id = JobId,
                Content = Content,
                Citations = JsonSerializer.Deserialize<List<ReportSource>>(Citations, Json) ?? [],
                Model = Model,
                Truncated = Truncated,
            };
            report.RestoreAudit(CreatedAt, CreatedBy, UpdatedAt, UpdatedBy);
            return report;
        }
    }
}
