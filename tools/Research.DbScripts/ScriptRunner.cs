using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Research.DbScripts;

/// <summary>
/// Applies scripts to one database. Each script runs in its own transaction together with its
/// <c>schema_scripts</c> row, so a failure leaves the database at the last good script. A session advisory lock
/// serializes concurrent runs (several AppHosts, a CI job). A script that was applied and has since changed on disk
/// stops the run: applied scripts are immutable; fix forward with a new script.
/// </summary>
internal static partial class ScriptRunner
{
    private const long AdvisoryLockKey = 0x5245_5345_4152_4348; // "RESEARCH"

    private const string CreateTableSql = """
        CREATE TABLE IF NOT EXISTS schema_scripts (
            name       text        PRIMARY KEY,
            checksum   char(64)    NOT NULL,
            applied_at timestamptz NOT NULL DEFAULT now()
        )
        """;

    public static async Task<int> ApplyAsync(
        string connectionString,
        IReadOnlyList<ScriptFile> scripts,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await ExecuteAsync(connection, null, "SELECT pg_advisory_lock(@key)", cancellationToken, ("key", AdvisoryLockKey));
        try
        {
            await ExecuteAsync(connection, null, CreateTableSql, cancellationToken);
            var appliedChecksums = await LoadAppliedAsync(connection, cancellationToken);

            var applied = 0;
            foreach (var script in scripts)
            {
                var sql = await File.ReadAllTextAsync(script.Path, cancellationToken);
                var checksum = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sql.ReplaceLineEndings("\n"))));

                if (appliedChecksums.TryGetValue(script.Name, out var existing))
                {
                    if (!string.Equals(existing.Trim(), checksum, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Script {script.Name} changed after it was applied. Applied scripts are immutable; add a new script instead.");
                    }

                    continue;
                }

                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                await ExecuteAsync(connection, transaction, sql, cancellationToken);
                await ExecuteAsync(
                    connection,
                    transaction,
                    "INSERT INTO schema_scripts (name, checksum) VALUES (@name, @checksum)",
                    cancellationToken,
                    ("name", script.Name),
                    ("checksum", checksum));
                await transaction.CommitAsync(cancellationToken);

                applied++;
                LogScriptApplied(logger, script.Name);
            }

            return applied;
        }
        finally
        {
            await ExecuteAsync(connection, null, "SELECT pg_advisory_unlock(@key)", CancellationToken.None, ("key", AdvisoryLockKey));
        }
    }

    private static async Task<Dictionary<string, string>> LoadAppliedAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var applied = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var command = new NpgsqlCommand("SELECT name, checksum FROM schema_scripts", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            applied[reader.GetString(0)] = reader.GetString(1);
        }

        return applied;
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "No connection string {ConnectionName}; skipping its scripts")]
    public static partial void LogSkippedDatabase(ILogger logger, string connectionName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "{ConnectionName}: {Applied} script(s) applied, {Total} known")]
    public static partial void LogDatabaseApplied(ILogger logger, string connectionName, int applied, int total);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "{ConnectionName}: applying scripts failed")]
    public static partial void LogDatabaseFailed(ILogger logger, Exception exception, string connectionName);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Applied {Script}")]
    private static partial void LogScriptApplied(ILogger logger, string script);
}
