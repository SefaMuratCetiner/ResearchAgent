namespace Research.Shared.Core.Mediation;

/// <summary>Handles a single request type <typeparamref name="TRequest"/> and produces <typeparamref name="TResponse"/>.</summary>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
