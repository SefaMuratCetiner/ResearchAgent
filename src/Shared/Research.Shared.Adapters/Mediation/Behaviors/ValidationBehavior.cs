using Research.Shared.Core.Mediation;
using Research.SharedKernel.Results;

namespace Research.Shared.Adapters.Mediation.Behaviors;

/// <summary>
/// Runs all registered <see cref="IRequestValidator{TRequest}"/> strategies before invoking the handler.
/// On the first validation error, constructs a failed <typeparamref name="TResponse"/> via
/// <see cref="IResultFactory{TSelf}"/> without throwing exceptions.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IRequestValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly Func<Error, TResponse>? FailureFactory = ResultFailureFactory.TryCreate<TResponse>();

    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        foreach (var validator in validators)
        {
            var error = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            if (error is not null)
            {
                if (FailureFactory is null)
                {
                    throw new InvalidOperationException(
                        $"Validator {validator.GetType().Name} rejected {typeof(TRequest).Name} with '{error.Code}', " +
                        $"but response type {typeof(TResponse).Name} does not implement IResultFactory<{typeof(TResponse).Name}>.");
                }

                return FailureFactory(error);
            }
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }
}

internal static class ResultFailureFactory
{
    public static Func<Error, TResponse>? TryCreate<TResponse>()
    {
        var responseType = typeof(TResponse);
        var factoryInterface = typeof(IResultFactory<>).MakeGenericType(responseType);
        if (!factoryInterface.IsAssignableFrom(responseType))
        {
            return null;
        }

        var invokerType = typeof(Invoker<>).MakeGenericType(responseType);
        var invoker = (IInvoker<TResponse>)Activator.CreateInstance(invokerType)!;
        return invoker.CreateFailure;
    }

    private interface IInvoker<out TResponse>
    {
        TResponse CreateFailure(Error error);
    }

    private sealed class Invoker<TResult> : IInvoker<TResult>
        where TResult : IOperationResult, IResultFactory<TResult>
    {
        public TResult CreateFailure(Error error) => TResult.CreateFailure(error);
    }
}
