using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Research.Hosting.Composition;
using Research.Providers.Postgres.Dapper;
using Research.Providers.Postgres.Health;
using Research.Providers.Postgres.Messaging;
using Research.Providers.Postgres.Transactions;
using Research.SharedKernel.Messaging;
using Research.SharedKernel.Transactions;

namespace Research.Knowledge.Host.Features;

/// <summary>
/// The <c>research_knowledge</c> database (Aspire connection <c>knowledge</c>, pgvector): transactions, outbox, inbox,
/// and a readiness check that the schema scripts ran.
/// </summary>
internal sealed class PostgresFeature : IResearchFeature
{
    private static readonly string[] RequiredTables =
        ["outbox_messages", "inbox_messages", "documents", "passages", "job_documents"];

    public void Register(ResearchBuilder app)
    {
        app.Host.AddNpgsqlDataSource("knowledge");
        DapperTypeHandlers.Register();

        app.Services.AddSingleton<ITransactionFactory, PostgresTransactionFactory>();
        app.Services.AddSingleton<IOutbox, PostgresOutbox>();
        app.Services.AddSingleton<IInbox, PostgresInbox>();
        app.Services.AddSingleton<IOutboxStore, PostgresOutboxStore>();

        app.Services.AddHealthChecks().Add(new HealthCheckRegistration(
            "knowledge-schema",
            static provider => new PostgresSchemaHealthCheck(provider.GetRequiredService<NpgsqlDataSource>(), RequiredTables),
            failureStatus: null,
            tags: ["ready"]));
    }
}
