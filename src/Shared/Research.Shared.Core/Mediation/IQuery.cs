namespace Research.Shared.Core.Mediation;

/// <summary>A read-only query returning <typeparamref name="TResponse"/>.</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;
