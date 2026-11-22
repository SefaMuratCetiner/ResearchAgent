namespace Research.Shared.Bus.Mediation;

/// <summary>Chain-of-responsibility middleware wrapping every <see cref="ISender.SendAsync{TResponse}"/> execution.</summary>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
