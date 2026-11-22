using Research.Providers.Postgres;
using Research.Shared.Core.Hosting;

namespace Research.Jobs.Worker.Features;

/// <summary>
/// The <c>research_jobs</c> database (Aspire connection <c>jobs</c>): data source with Aspire's health check and
/// tracing, scoped unit of work, outbox, inbox, and a readiness check that the schema scripts ran.
/// </summary>
internal sealed class PostgresFeature : IHostFeature
{
    private static readonly string[] RequiredTables =
        ["outbox_messages", "inbox_messages", "research_jobs", "job_steps", "research_reports"];

    public void Register(ServiceHostBuilder app)
    {
        app.Host.AddNpgsqlDataSource("jobs");
        app.Services.AddPostgresProvider("jobs-schema", RequiredTables);
    }
}
