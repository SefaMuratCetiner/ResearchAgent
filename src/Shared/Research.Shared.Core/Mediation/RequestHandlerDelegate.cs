namespace Research.Shared.Core.Mediation;

/// <summary>Represents the next step in the request pipeline (either the next behavior or the terminal handler).</summary>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken);
