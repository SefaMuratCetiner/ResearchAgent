using Research.Shared.Core.Logging;

namespace Research.Shared.Adapters.Http;

/// <summary>
/// Propagates the ambient <see cref="CorrelationContext.Current"/> value onto outgoing inter-service HTTP requests as
/// <c>X-Correlation-ID</c> when the header is not already set.
/// </summary>
public sealed class CorrelationIdDelegatingHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (CorrelationContext.Current is { } correlationId
            && !request.Headers.Contains(CorrelationContext.HeaderName))
        {
            request.Headers.TryAddWithoutValidation(CorrelationContext.HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
