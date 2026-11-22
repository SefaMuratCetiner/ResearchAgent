using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Research.Shared.Core.Logging;

namespace Research.Shared.Web.Middleware;

/// <summary>
/// Accepts an incoming <c>X-Correlation-ID</c> (a caller's business reference, forwarded by the gateway) when it is
/// well formed, puts it in <see cref="CorrelationContext"/>, the log scope and on the span, and echoes it back. Trace
/// context itself is handled by OpenTelemetry from <c>traceparent</c>; this id is only a cross-reference.
/// </summary>
public sealed partial class CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
{
    public const string HeaderName = CorrelationContext.HeaderName;

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var value = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(value))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (!CorrelationContext.IsValid(value))
        {
            LogRejected(logger, value.Length);
            await next(context).ConfigureAwait(false);
            return;
        }

        context.Features.Set(new CorrelationIdFeature(value));
        Activity.Current?.SetTag("research.correlation.id", value);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = value;
            return Task.CompletedTask;
        });

        using (CorrelationContext.Push(value))
        using (logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal) { ["CorrelationId"] = value }))
        {
            await next(context).ConfigureAwait(false);
        }
    }

    [LoggerMessage(EventId = 9200, Level = LogLevel.Debug, Message = "Ignored a malformed correlation id of length {Length}")]
    private static partial void LogRejected(ILogger logger, int length);
}

/// <summary>The accepted correlation id of the current request.</summary>
public sealed record CorrelationIdFeature(string Value);
