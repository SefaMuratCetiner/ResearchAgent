using Research.Shared.Core.Logging;
using Research.Shared.Core.Correlation;

namespace Research.Shared.Adapters.Http;

/// <summary>
/// Propagates the ambient <see cref="CorrelationIdContext.Current"/> value onto outgoing inter-service HTTP requests as
/// <c>X-Correlation-ID</c> when the header is not already set.
/// </summary>
public sealed class CorrelationIdDelegatingHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (CorrelationIdContext.Current is { } correlationId
            && !request.Headers.Contains(CorrelationIdContext.HeaderName))
        {
            request.Headers.TryAddWithoutValidation(CorrelationIdContext.HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
