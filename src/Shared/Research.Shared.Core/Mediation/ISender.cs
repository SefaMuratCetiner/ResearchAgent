namespace Research.Shared.Core.Mediation;

/// <summary>Dispatches a command, query, or integration message request through the configured pipeline to its single handler.</summary>
public interface ISender
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken);

    Task SendAsync(ICommand command, CancellationToken cancellationToken);
}
