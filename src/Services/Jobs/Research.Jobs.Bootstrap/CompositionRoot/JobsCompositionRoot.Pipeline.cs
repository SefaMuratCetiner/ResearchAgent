using Research.Shared.Web.Middleware;

namespace Research.Jobs.Bootstrap.CompositionRoot;

public sealed partial class JobsCompositionRoot
{
    /// <summary>
    /// The middleware order, outermost first. Every request passes these in order; responses pass them in reverse.
    /// </summary>
    public void ComposePipeline(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // 1. The caller's correlation id, before anything logs.
        app.UseMiddleware<CorrelationMiddleware>();

        // 2. Content log of request and response bodies; outside the error handler so it sees problem responses too.
        app.UseMiddleware<HttpContentLoggingMiddleware>();

        // 3. Last resort: ASP.NET Core IExceptionHandler (CustomExceptionHandler) turns unhandled exceptions into 500 ProblemDetails.
        app.UseExceptionHandler();
    }
}
