using Npgsql;
using Research.Providers.Postgres.Dapper;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Research.Infrastructure.IntegrationTests.Postgres.PostgresFixture))]

namespace Research.Infrastructure.IntegrationTests.Postgres;

/// <summary>
/// One pgvector container for the run, with the three service databases created and the repository's schema
/// scripts applied in the same order <c>tools/Research.DbScripts</c> uses. Tests isolate by using fresh job ids.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .Build();

    private readonly Dictionary<string, NpgsqlDataSource> _dataSources = new(StringComparer.Ordinal);

    public NpgsqlDataSource Jobs => _dataSources["jobs"];

    public NpgsqlDataSource Fetching => _dataSources["fetching"];

    public NpgsqlDataSource Knowledge => _dataSources["knowledge"];

    public async ValueTask InitializeAsync()
    {
        DapperTypeHandlers.Register();
        await _container.StartAsync();

        var databases = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["jobs"] = ["src/Providers/Research.Providers.Postgres/db", "src/Services/Jobs/Research.Jobs.Infrastructure/Persistence/db"],
            ["fetching"] = ["src/Providers/Research.Providers.Postgres/db", "src/Services/Fetching/Research.Fetching.Infrastructure/Persistence/db"],
            ["knowledge"] = ["src/Providers/Research.Providers.Postgres/db", "src/Services/Knowledge/Research.Knowledge.Infrastructure/Persistence/db"],
        };

        await using (var admin = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await admin.OpenAsync();
            foreach (var name in databases.Keys)
            {
                await using var create = new NpgsqlCommand($"CREATE DATABASE research_{name}", admin);
                await create.ExecuteNonQueryAsync();
            }
        }

        foreach (var (name, folders) in databases)
        {
            var connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = $"research_{name}" }.ConnectionString;
            var dataSource = NpgsqlDataSource.Create(connectionString);
            _dataSources[name] = dataSource;

            var scripts = folders
                .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(RepositoryRoot.Path, folder), "*.sql"))
                .OrderBy(Path.GetFileName, StringComparer.Ordinal);

            await using var connection = await dataSource.OpenConnectionAsync();
            foreach (var script in scripts)
            {
                await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(script), connection);
                await command.ExecuteNonQueryAsync();
            }

            // pgvector types are created by the script; refresh the connection's type cache for later connections.
            await connection.ReloadTypesAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var dataSource in _dataSources.Values)
        {
            await dataSource.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}

internal static class RepositoryRoot
{
    public static string Path { get; } = Find();

    private static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "Research.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Research.slnx not found");
    }
}
