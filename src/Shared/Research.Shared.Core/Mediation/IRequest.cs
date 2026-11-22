using Research.SharedKernel.Results;

namespace Research.Shared.Core.Mediation;

/// <summary>Marker for an in-process request handled by a single <see cref="IRequestHandler{TRequest, TResponse}"/>.</summary>
public interface IRequest<out TResponse>;

/// <summary>A state-changing use-case command returning <typeparamref name="TResponse"/>.</summary>
public interface ICommand<out TResponse> : IRequest<TResponse>;

/// <summary>A state-changing use-case command that returns no value.</summary>
public interface ICommand : ICommand<Unit>;

/// <summary>A read-only query returning <typeparamref name="TResponse"/>.</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;

/// <summary>
/// Opt-in marker for commands whose entire execution should be wrapped in a single <see cref="Transactions.IUnitOfWork"/>
/// transaction by <c>TransactionBehavior</c>, committing on success and rolling back on failure or exception.
/// </summary>
public interface ITransactionalCommand;
