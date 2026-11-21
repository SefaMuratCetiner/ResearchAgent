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
        var createdAt = report.CreatedAt == default ? unitOfWork.Now : report.CreatedAt;
        var createdBy = string.IsNullOrWhiteSpace(report.CreatedBy) ? unitOfWork.ActorId : report.CreatedBy;

        await unitOfWork.ActiveConnection.ExecuteAsync(new CommandDefinition(
            InsertSql,
            new
            {
                JobId = report.Id,
                report.Content,
                Citations = JsonSerializer.Serialize(report.Citations, Json),
                report.Model,
                report.Truncated,
                CreatedAt = createdAt,
                CreatedBy = createdBy,
            },
            unitOfWork.ActiveTransaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<ResearchReport?> GetAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var connection = await unitOfWork.DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<ReportRow>(new CommandDefinition(
            SelectSql,
            new { JobId = jobId },
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

        public ResearchReport ToDomain() => new()
        {
            Id = JobId,
            Content = Content,
            Citations = JsonSerializer.Deserialize<List<ReportSource>>(Citations, Json) ?? [],
            Model = Model,
            Truncated = Truncated,
            CreatedAt = CreatedAt,
            CreatedBy = CreatedBy,
            UpdatedAt = UpdatedAt,
            UpdatedBy = UpdatedBy,
        };
    }
}
