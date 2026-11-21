using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Research.Providers.Postgres.Dapper;
using Research.Providers.Postgres.Health;
using Research.Providers.Postgres.Messaging;
using Research.Providers.Postgres.Transactions;
using Research.ServiceDefaults.Composition;
using Research.Shared.Adapters.Events;
using Research.Shared.Core.Events;
using Research.Shared.Core.Messaging;
using Research.Shared.Core.Transactions;

namespace Research.Knowledge.Bootstrap.Features;

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

        app.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        app.Services.AddScoped<ITransactionFactory, PostgresTransactionFactory>();
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
