using Microsoft.AspNetCore.Http;

namespace Research.Shared.Web.Endpoints;

/// <summary>
/// Stage-1 HTTP transport validator: checks syntactic/shape rules (missing body, required fields, HTTP header format)
/// before an endpoint handler executes. Application and policy rules belong in <c>IRequestValidator&lt;T&gt;</c>.
/// </summary>
public interface IEndpointValidator<in TRequest>
{
    void Validate(TRequest? request, HttpContext httpContext, IDictionary<string, string[]> errors);
}
