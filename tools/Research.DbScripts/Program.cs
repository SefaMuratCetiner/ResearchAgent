using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Research.DbScripts;

// Applies every database's scripts and exits: 0 when all databases are up to date, 1 otherwise. The AppHost starts
// the services only after this exits with 0.
var builder = Host.CreateApplicationBuilder(args);
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Research.DbScripts");
var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
var cancellationToken = lifetime.ApplicationStopping;

var scriptsRoot = Path.Combine(AppContext.BaseDirectory, "db");
var failed = false;

foreach (var database in DatabaseScripts.All)
{
    var connectionString = builder.Configuration.GetConnectionString(database.ConnectionName);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        // Not every environment has every database (a developer may run one service); skip what is not configured.
        ScriptRunner.LogSkippedDatabase(logger, database.ConnectionName);
        continue;
    }

    try
    {
        var scripts = database.Folders
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(scriptsRoot, folder), "*.sql"))
            .Select(path => new ScriptFile(Path.GetFileName(path), path))
            .OrderBy(static s => s.Name, StringComparer.Ordinal)
            .ToList();

        var applied = await ScriptRunner.ApplyAsync(connectionString, scripts, logger, cancellationToken);
        ScriptRunner.LogDatabaseApplied(logger, database.ConnectionName, applied, scripts.Count);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        failed = true;
        ScriptRunner.LogDatabaseFailed(logger, exception, database.ConnectionName);
    }
}

return failed ? 1 : 0;
