namespace Research.Shared.Core.Mediation;

/// <summary>Marker for an in-process request handled by a single <see cref="IRequestHandler{TRequest, TResponse}"/>.</summary>
public interface IRequest<out TResponse>;
