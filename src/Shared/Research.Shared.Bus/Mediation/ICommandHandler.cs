using Research.Shared.Kernel.Results;

namespace Research.Shared.Bus.Mediation;

/// <summary>Handles a command returning <typeparamref name="TResponse"/>.</summary>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>;

/// <summary>Handles a command with no return value.</summary>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Unit>
    where TCommand : ICommand;
