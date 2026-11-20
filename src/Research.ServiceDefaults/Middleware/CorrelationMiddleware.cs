using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Research.ServiceDefaults.Middleware;

/// <summary>
/// Accepts an incoming <c>X-Correlation-ID</c> (a caller's business reference, forwarded by the gateway) when it is
/// well formed, puts it in the log scope and on the span, and echoes it back. Trace context itself is handled by
/// OpenTelemetry from <c>traceparent</c>; this id is only a cross-reference.
/// </summary>
public sealed partial class CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var value = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(value))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (!IsValid(value))
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

        using (logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal) { ["CorrelationId"] = value }))
        {
            await next(context).ConfigureAwait(false);
        }
    }

    private static bool IsValid(string value) =>
        value.Length <= MaxLength
        && value.All(static c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');

    [LoggerMessage(EventId = 9200, Level = LogLevel.Debug, Message = "Ignored a malformed correlation id of length {Length}")]
    private static partial void LogRejected(ILogger logger, int length);
}

/// <summary>The accepted correlation id of the current request.</summary>
public sealed record CorrelationIdFeature(string Value);
