using Research.Providers.Postgres;
using Research.Shared.Adapters.Composition;

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
        app.Services.AddPostgresProvider("knowledge-schema", RequiredTables);
    }
}
