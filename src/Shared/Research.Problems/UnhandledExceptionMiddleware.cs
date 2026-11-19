using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Research.Problems;

/// <summary>
/// The last resort: an exception no endpoint handled becomes <c>500 research.unexpected</c>. The exception is logged
/// and recorded on the span; the response carries the trace id only. Client disconnects are not errors.
/// </summary>
public sealed partial class UnhandledExceptionMiddleware(RequestDelegate next, ILogger<UnhandledExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            LogClientDisconnected(logger, context.Request.Path);
        }
#pragma warning disable CA1031 // Every exception must become a problem document; nothing else may reach the client.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogUnhandled(logger, exception, context.Request.Method, context.Request.Path);
            Activity.Current?.AddException(exception);
            Activity.Current?.SetStatus(ActivityStatusCode.Error);

            if (context.Response.HasStarted)
            {
                return;
            }

            context.Response.Clear();
            var problem = ProblemResults.Create(ProblemErrors.Unexpected, context, detail: null, errors: null);
            context.Response.StatusCode = problem.Status;
            context.Response.ContentType = ResearchProblem.ContentType;
            await JsonSerializer.SerializeAsync(context.Response.Body, problem, Json, context.RequestAborted).ConfigureAwait(false);
        }
    }

    [LoggerMessage(EventId = 9100, Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, string path);

    [LoggerMessage(EventId = 9101, Level = LogLevel.Information, Message = "Client disconnected from {Path}")]
    private static partial void LogClientDisconnected(ILogger logger, string path);
}
