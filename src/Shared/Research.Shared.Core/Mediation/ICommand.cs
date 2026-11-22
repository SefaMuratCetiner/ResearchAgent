using Research.Shared.Kernel.Results;

namespace Research.Shared.Core.Mediation;

/// <summary>A state-changing use-case command returning <typeparamref name="TResponse"/>.</summary>
public interface ICommand<out TResponse> : IRequest<TResponse>;

/// <summary>A state-changing use-case command that returns no value.</summary>
public interface ICommand : ICommand<Unit>;
