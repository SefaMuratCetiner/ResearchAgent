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

namespace Research.Fetching.Bootstrap.Features;

/// <summary>
/// The <c>research_fetching</c> database (Aspire connection <c>fetching</c>): transactions, outbox, inbox, and a
/// readiness check that the schema scripts ran.
/// </summary>
internal sealed class PostgresFeature : IResearchFeature
{
    private static readonly string[] RequiredTables = ["outbox_messages", "inbox_messages", "fetched_documents"];

    public void Register(ResearchBuilder app)
    {
        app.Host.AddNpgsqlDataSource("fetching");
        DapperTypeHandlers.Register();

        app.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        app.Services.AddScoped<ITransactionFactory, PostgresTransactionFactory>();
        app.Services.AddSingleton<IOutbox, PostgresOutbox>();
        app.Services.AddSingleton<IInbox, PostgresInbox>();
        app.Services.AddSingleton<IOutboxStore, PostgresOutboxStore>();

        app.Services.AddHealthChecks().Add(new HealthCheckRegistration(
            "fetching-schema",
            static provider => new PostgresSchemaHealthCheck(provider.GetRequiredService<NpgsqlDataSource>(), RequiredTables),
            failureStatus: null,
            tags: ["ready"]));
    }
}
