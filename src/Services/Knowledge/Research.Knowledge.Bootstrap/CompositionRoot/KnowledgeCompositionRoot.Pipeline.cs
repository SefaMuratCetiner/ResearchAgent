using Research.Shared.Web.Middleware;

namespace Research.Knowledge.Bootstrap.CompositionRoot;

public sealed partial class KnowledgeCompositionRoot
{
    /// <summary>
    /// The middleware order, outermost first. Every request passes these in order; responses pass them in reverse.
    /// </summary>
    public void ComposePipeline(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // 1. The caller's correlation id, before anything logs.
        app.UseMiddleware<CorrelationIdMiddleware>();

        // 2. Content log of request and response bodies (queries and returned passages).
        app.UseMiddleware<HttpContentLoggingMiddleware>();

        // 3. Last resort: ASP.NET Core IExceptionHandler (GlobalExceptionHandler) turns unhandled exceptions into 500 ProblemDetails.
        app.UseExceptionHandler();
    }
}
