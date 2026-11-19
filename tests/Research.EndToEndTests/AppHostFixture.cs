using Aspire.Hosting;
using Aspire.Hosting.Testing;

[assembly: AssemblyFixture(typeof(Research.EndToEndTests.AppHostFixture))]

namespace Research.EndToEndTests;

/// <summary>Starts the whole topology once per run and waits until Jobs and Knowledge are healthy.</summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(5);

    private DistributedApplication? _app;

    /// <summary>The real key when set; otherwise a dummy, so everything that does not call Claude still works.</summary>
    public static string? AnthropicApiKey { get; } = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

    public HttpClient Jobs { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        using var timeout = new CancellationTokenSource(StartTimeout);
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Research_AppHost>(timeout.Token);
        builder.Configuration["Parameters:anthropic-api-key"] = AnthropicApiKey ?? "e2e-dummy-key";

        _app = await builder.BuildAsync(timeout.Token);
        await _app.StartAsync(timeout.Token);

        await _app.ResourceNotifications.WaitForResourceHealthyAsync("jobs", timeout.Token);
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("knowledge", timeout.Token);

        Jobs = _app.CreateHttpClient("jobs");
    }

    public async ValueTask DisposeAsync()
    {
        Jobs?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
