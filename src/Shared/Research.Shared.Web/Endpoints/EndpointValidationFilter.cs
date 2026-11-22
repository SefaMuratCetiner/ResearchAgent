using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Research.Shared.Web.Problems;

namespace Research.Shared.Web.Endpoints;

/// <summary>
/// Minimal API <see cref="IEndpointFilter"/> that runs an <see cref="IEndpointValidator{TRequest}"/> before the
/// endpoint delegate executes, short-circuiting invalid HTTP requests with RFC 9457
/// <see cref="ProblemResults.Validation"/>.
/// </summary>
public sealed class EndpointValidationFilter<TRequest>(IEndpointValidator<TRequest>? validator = null) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var activeValidator = validator ?? context.HttpContext.RequestServices.GetRequiredService<IEndpointValidator<TRequest>>();
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        activeValidator.Validate(request, context.HttpContext, errors);
        if (errors.Count > 0)
        {
            return ProblemResults.Validation(errors, context.HttpContext);
        }

        return await next(context).ConfigureAwait(false);
    }
}
