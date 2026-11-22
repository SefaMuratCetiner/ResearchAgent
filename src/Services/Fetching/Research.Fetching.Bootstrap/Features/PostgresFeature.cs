using Research.Providers.Postgres;
using Research.Shared.Adapters.Composition;

namespace Research.Fetching.Bootstrap.Features;

/// <summary>
/// The <c>research_fetching</c> database (Aspire connection <c>fetching</c>): scoped unit of work, outbox, inbox,
/// and a readiness check that the schema scripts ran.
/// </summary>
internal sealed class PostgresFeature : IHostFeature
{
    private static readonly string[] RequiredTables = ["outbox_messages", "inbox_messages", "fetched_documents"];

    public void Register(ServiceHostBuilder app)
    {
        app.Host.AddNpgsqlDataSource("fetching");
        app.Services.AddPostgresProvider("fetching-schema", RequiredTables);
    }
}
