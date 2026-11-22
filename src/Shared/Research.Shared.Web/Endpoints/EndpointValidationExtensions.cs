using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Research.Shared.Web.Endpoints;

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
