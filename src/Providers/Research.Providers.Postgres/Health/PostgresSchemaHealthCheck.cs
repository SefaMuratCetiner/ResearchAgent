using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Research.Providers.Postgres.Health;

/// <summary>
/// Ready only when the service's tables exist: schema scripts run before the services (no migrations at startup), so
/// a missing table means the scripts have not been applied to this database.
/// </summary>
public sealed class PostgresSchemaHealthCheck(NpgsqlDataSource dataSource, IReadOnlyList<string> requiredTables) : IHealthCheck
{
    private const string Sql = """
        SELECT table_name FROM information_schema.tables
        WHERE table_schema = current_schema() AND table_name = ANY(@Tables)
        """;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            var found = (await connection.QueryAsync<string>(new CommandDefinition(
                Sql,
                new { Tables = requiredTables.ToArray() },
                cancellationToken: cancellationToken)).ConfigureAwait(false)).ToHashSet(StringComparer.Ordinal);

            var missing = requiredTables.Where(table => !found.Contains(table)).ToList();
            return missing.Count == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Missing tables: " + string.Join(", ", missing));
        }
        catch (NpgsqlException exception)
        {
            return HealthCheckResult.Unhealthy("The database is unreachable.", exception);
        }
    }
}
