using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Research.Shared.Web.Problems;

namespace Research.Shared.Web.Endpoints;

/// <summary>
/// Stage-1 HTTP transport validator: checks syntactic/shape rules (missing body, required fields, HTTP header format)
/// before an endpoint handler executes. Application and policy rules belong in <c>IRequestValidator&lt;T&gt;</c>.
/// </summary>
public interface IEndpointValidator<in TRequest>
{
    void Validate(TRequest? request, HttpContext httpContext, IDictionary<string, string[]> errors);
}

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

/// <summary>Route-builder extensions for attaching Stage-1 HTTP request validation filters to Minimal API endpoints.</summary>
public static class EndpointValidationExtensions
{
    /// <summary>Validates <typeparamref name="TRequest"/> using a stateless <typeparamref name="TValidator"/> before the endpoint runs.</summary>
    public static RouteHandlerBuilder WithRequestValidation<TRequest, TValidator>(this RouteHandlerBuilder builder)
        where TValidator : IEndpointValidator<TRequest>, new()
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddEndpointFilter(new EndpointValidationFilter<TRequest>(new TValidator()));
    }

    /// <summary>Validates <typeparamref name="TRequest"/> using a DI-registered <see cref="IEndpointValidator{TRequest}"/> before the endpoint runs.</summary>
    public static RouteHandlerBuilder WithRequestValidation<TRequest>(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddEndpointFilter(new EndpointValidationFilter<TRequest>());
    }
}
