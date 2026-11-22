using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Debug;
using OpenTelemetry.Logs;
using Research.ServiceDefaults;
using Research.Shared.Core.Http;
using Research.Shared.Core.Logging;
using Research.Shared.Web.Middleware;
using Research.Shared.Core.Correlation;

namespace Research.Infrastructure.UnitTests.ServiceDefaults;

public sealed class LoggingDefaultsTests
{
    [Fact]
    public void AddServiceDefaults_ClearsDefaultMelProviders_LeavingOnlyOpenTelemetryAsMelProvider()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["OTEL_SERVICE_NAME"] = "jobs",
            ["OTEL_RESOURCE_ATTRIBUTES"] = "service.instance.id=jobs-replica-1",
        });

        builder.AddServiceDefaults();

        using var host = builder.Build();
        var providers = host.Services.GetServices<ILoggerProvider>().ToList();

        providers.ShouldNotContain(static p => p is ConsoleLoggerProvider);
        providers.ShouldNotContain(static p => p is DebugLoggerProvider);
        providers.ShouldContain(static p => p is OpenTelemetryLoggerProvider);
    }

    [Theory]
    [InlineData("service.instance.id=jobs-abc123", "jobs-abc123")]
    [InlineData("deployment.environment=prod, service.instance.id=search-02, host.name=node1", "search-02")]
    [InlineData("service.instance.id=jobs%2D01", "jobs-01")]
    public void ResolveInstanceId_ExtractsServiceInstanceIdFromOtelResourceAttributes(string attributes, string expected) =>
        ServiceDefaultsExtensions.ResolveInstanceId(attributes).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("host.name=node1")]
    public void ResolveInstanceId_FallsBackToMachineAndProcessId(string? attributes)
    {
        var instanceId = ServiceDefaultsExtensions.ResolveInstanceId(attributes);
        instanceId.ShouldBe($"{Environment.MachineName}:{Environment.ProcessId}");
    }

    [Fact]
    public void CreateBootstrapLogger_ReturnsUsableReloadableLogger()
    {
        using var logger = ServiceDefaultsExtensions.CreateBootstrapLogger("jobs");
        logger.ShouldNotBeNull();
    }

    [Fact]
    public async Task CorrelationMiddleware_SetsAmbientContextLogScopeAndSpanTag_AndRestoresAfterRequest()
    {
        var capturedScopes = new List<IReadOnlyDictionary<string, object>>();
        var logger = new ScopeCapturingLogger<CorrelationIdMiddleware>(capturedScopes);
        string? observedInside = null;

        var middleware = new CorrelationIdMiddleware(
            _ =>
            {
                observedInside = CorrelationIdContext.Current;
                return Task.CompletedTask;
            },
            logger);

        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdContext.HeaderName] = "req-42:abc";

        using var activity = new Activity("test-http").Start();
        await middleware.InvokeAsync(context);

        observedInside.ShouldBe("req-42:abc");
        CorrelationIdContext.Current.ShouldBeNull();
        context.Features.Get<CorrelationIdFeature>()?.Value.ShouldBe("req-42:abc");
        activity.GetTagItem("research.correlation.id").ShouldBe("req-42:abc");
        capturedScopes.ShouldContain(static s => Equals(s["CorrelationId"], "req-42:abc"));
    }

    [Fact]
    public async Task CorrelationMiddleware_IgnoresMalformedHeader()
    {
        var capturedScopes = new List<IReadOnlyDictionary<string, object>>();
        var logger = new ScopeCapturingLogger<CorrelationIdMiddleware>(capturedScopes);
        string? observedInside = null;

        var middleware = new CorrelationIdMiddleware(
            _ =>
            {
                observedInside = CorrelationIdContext.Current;
                return Task.CompletedTask;
            },
            logger);

        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdContext.HeaderName] = "bad value with spaces!";

        await middleware.InvokeAsync(context);

        observedInside.ShouldBeNull();
        context.Features.Get<CorrelationIdFeature>().ShouldBeNull();
        capturedScopes.ShouldBeEmpty();
    }

    [Fact]
    public async Task CorrelationIdDelegatingHandler_ForwardsAmbientCorrelationId_WithoutOverwritingExplicitHeader()
    {
        var recorder = new RecordingHandler();
        using var handler = new CorrelationIdDelegatingHandler { InnerHandler = recorder };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://knowledge.local") };

        using (CorrelationIdContext.Push("corr-99"))
        {
            using var response = await client.GetAsync(new Uri("/passages", UriKind.Relative), TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        recorder.LastHeaders.ShouldContainKeyAndValue(CorrelationIdContext.HeaderName, "corr-99");

        using (CorrelationIdContext.Push("corr-99"))
        {
            using var explicitRequest = new HttpRequestMessage(HttpMethod.Get, "/passages");
            explicitRequest.Headers.Add(CorrelationIdContext.HeaderName, "explicit-1");
            using var response = await client.SendAsync(explicitRequest, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        recorder.LastHeaders.ShouldContainKeyAndValue(CorrelationIdContext.HeaderName, "explicit-1");
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Dictionary<string, string> LastHeaders { get; } = new(StringComparer.OrdinalIgnoreCase);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastHeaders.Clear();
            foreach (var header in request.Headers)
            {
                LastHeaders[header.Key] = string.Join(",", header.Value);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class ScopeCapturingLogger<T>(List<IReadOnlyDictionary<string, object>> scopes) : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            if (state is IReadOnlyDictionary<string, object> dict)
            {
                scopes.Add(dict);
            }

            return NullDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
