using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Research.ServiceDefaults.Problems;

namespace Research.ServiceDefaults.Exceptions;

/// <summary>
/// Global <see cref="IExceptionHandler"/> backed by ASP.NET Core's <see cref="IProblemDetailsService"/>:
/// unhandled exceptions become <c>500 research.unexpected</c> RFC 9457 ProblemDetails responses carrying only the
/// trace id. Client disconnects are logged at Information level and not treated as server errors.
/// </summary>
public sealed partial class CustomExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<CustomExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            LogClientDisconnected(logger, httpContext.Request.Path);
            return true;
        }

        LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path);
        Activity.Current?.AddException(exception);
        Activity.Current?.SetStatus(ActivityStatusCode.Error);

        if (httpContext.Response.HasStarted)
        {
            return true;
        }

        var error = ProblemErrors.Unexpected;
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var problemDetails = new ProblemDetails
        {
            Type = ProblemResults.TypeBase + error.Code,
            Title = error.Title,
            Status = StatusCodes.Status500InternalServerError,
            Instance = httpContext.Request.Path.Value,
        };
        problemDetails.Extensions["code"] = error.Code;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        }).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 9100, Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, string path);

    [LoggerMessage(EventId = 9101, Level = LogLevel.Information, Message = "Client disconnected from {Path}")]
    private static partial void LogClientDisconnected(ILogger logger, string path);
}
