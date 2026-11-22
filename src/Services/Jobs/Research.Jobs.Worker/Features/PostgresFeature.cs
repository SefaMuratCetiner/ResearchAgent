using Research.Providers.Postgres.Dapper;
using Research.Providers.Postgres.Health;
using Research.Providers.Postgres.Messaging;
using Research.Providers.Postgres.Transactions;
using Research.Shared.Adapters.Composition;
using Research.Shared.Messaging;
using Research.Shared.Core.Transactions;

namespace Research.Jobs.Worker.Features;

/// <summary>
/// The <c>research_jobs</c> database (Aspire connection <c>jobs</c>): data source with Aspire's health check and
/// tracing, scoped unit of work, outbox, inbox, and a readiness check that the schema scripts ran.
/// </summary>
internal sealed class PostgresFeature : IResearchFeature
{
    private static readonly string[] RequiredTables =
        ["outbox_messages", "inbox_messages", "research_jobs", "job_steps", "research_reports"];

    public void Register(ResearchBuilder app)
    {
        app.Host.AddNpgsqlDataSource("jobs");
        DapperTypeHandlers.Register();

        app.Services.AddScoped<PostgresUnitOfWork>();
        app.Services.AddScoped<IUnitOfWork>(static provider => provider.GetRequiredService<PostgresUnitOfWork>());
        app.Services.AddScoped<IOutbox, PostgresOutbox>();
        app.Services.AddScoped<IInbox, PostgresInbox>();
        app.Services.AddSingleton<IOutboxStore, PostgresOutboxStore>();

        app.Services.AddHealthChecks().Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
            "jobs-schema",
            static provider => new PostgresSchemaHealthCheck(provider.GetRequiredService<Npgsql.NpgsqlDataSource>(), RequiredTables),
            failureStatus: null,
            tags: ["ready"]));
    }
}
