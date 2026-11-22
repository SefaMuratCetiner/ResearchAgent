using Microsoft.Extensions.DependencyInjection;
using Research.Shared.Bus.Mediation;
using Research.Shared.Bus.Transactions;
using Research.Shared.Kernel.Results;

namespace Research.Shared.Bus.Transactions;

/// <summary>
/// Wraps any request marked with <see cref="ITransactionalCommand"/> in a single <see cref="IUnitOfWork"/> transaction,
/// committing only when execution completes without a failed <see cref="IOperationResult"/> or exception.
/// </summary>
internal sealed class TransactionBehavior<TRequest, TResponse>(
    IServiceProvider serviceProvider) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly bool IsTransactional = typeof(ITransactionalCommand).IsAssignableFrom(typeof(TRequest));

    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!IsTransactional)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
        await using var _ = await unitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);

        var response = await next(cancellationToken).ConfigureAwait(false);
        if (response is IOperationResult { IsSuccess: false })
        {
            return response;
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        return response;
    }
}
