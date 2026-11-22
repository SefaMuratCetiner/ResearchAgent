using Microsoft.AspNetCore.Http;
using Research.Shared.Web.Endpoints;

namespace Research.Jobs.Api.CreateResearchJob;

/// <summary>Stage-1 HTTP validation: checks required body field and <c>Idempotency-Key</c> header syntax before the endpoint runs.</summary>
public sealed class CreateResearchJobRequestValidator : IEndpointValidator<CreateResearchJobRequest>
{
    private const int MaxIdempotencyKeyLength = 200;

    public void Validate(CreateResearchJobRequest? request, HttpContext httpContext, IDictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(request?.Question))
        {
            errors["question"] = ["The question is required."];
        }

        if (httpContext.Request.Headers.TryGetValue(CreateResearchJobEndpoint.IdempotencyKeyHeader, out var values))
        {
            var key = values.ToString();
            if (key.Length is 0 or > MaxIdempotencyKeyLength || key.Any(static c => c is < '!' or > '~'))
            {
                errors[CreateResearchJobEndpoint.IdempotencyKeyHeader] = [$"Use 1 to {MaxIdempotencyKeyLength} visible ASCII characters."];
            }
        }
    }
}
