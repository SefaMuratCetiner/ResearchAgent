namespace Research.Shared.Bus.Mediation;

/// <summary>Handles a read-only query returning <typeparamref name="TResponse"/>.</summary>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>;
