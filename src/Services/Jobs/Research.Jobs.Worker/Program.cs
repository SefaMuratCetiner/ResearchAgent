using System.Globalization;
using Research.Jobs.Worker.CompositionRoot;
using Serilog;

// Bootstrap logger, so startup failures still reach the console. LoggingFeature replaces it with the configured
// logger once the host is built.
Log.Logger = new LoggerConfiguration().WriteTo.Console(formatProvider: CultureInfo.InvariantCulture).CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    var root = new JobsWorkerCompositionRoot(builder);
    root.ComposeServices();

    var app = builder.Build();
    root.ComposeEndpoints(app);

    await app.RunAsync();
    return 0;
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "The Jobs Worker service terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Entry point; public so integration tests can host the real composition root.</summary>
public partial class Program;
