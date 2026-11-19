using Research.Synthesis.Host.CompositionRoot;
using Serilog;

// Bootstrap logger, so startup failures still reach the console. LoggingFeature replaces it once the host is built.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    var root = new SynthesisCompositionRoot(builder);
    root.ComposeServices();

    var app = builder.Build();
    root.ComposeEndpoints(app);

    await app.RunAsync();
    return 0;
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "The Synthesis service terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Entry point; public so integration tests can host the real composition root.</summary>
public partial class Program;
