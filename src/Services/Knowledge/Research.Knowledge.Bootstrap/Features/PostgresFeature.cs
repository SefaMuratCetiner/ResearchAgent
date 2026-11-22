using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Research.Providers.Postgres.Dapper;
using Research.Providers.Postgres.Health;
using Research.Providers.Postgres.Messaging;
using Research.Providers.Postgres.Transactions;
using Research.Shared.Adapters.Composition;
using Research.Shared.Core.Messaging;
using Research.Shared.Core.Transactions;

namespace Research.Knowledge.Bootstrap.Features;

/// <summary>
/// The <c>research_knowledge</c> database (Aspire connection <c>knowledge</c>, pgvector): scoped unit of work,
/// outbox, inbox, and a readiness check that the schema scripts ran.
/// </summary>
internal sealed class PostgresFeature : IResearchFeature
{
    private static readonly string[] RequiredTables =
        ["outbox_messages", "inbox_messages", "documents", "passages", "job_documents"];

    public void Register(ResearchBuilder app)
    {
        app.Host.AddNpgsqlDataSource("knowledge");
        DapperTypeHandlers.Register();

        app.Services.AddScoped<PostgresUnitOfWork>();
        app.Services.AddScoped<IUnitOfWork>(static provider => provider.GetRequiredService<PostgresUnitOfWork>());
        app.Services.AddScoped<IOutbox, PostgresOutbox>();
        app.Services.AddScoped<IInbox, PostgresInbox>();
        app.Services.AddSingleton<IOutboxStore, PostgresOutboxStore>();

        app.Services.AddHealthChecks().Add(new HealthCheckRegistration(
            "knowledge-schema",
            static provider => new PostgresSchemaHealthCheck(provider.GetRequiredService<NpgsqlDataSource>(), RequiredTables),
            failureStatus: null,
            tags: ["ready"]));
    }
}
