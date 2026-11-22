using Research.SharedKernel.Events;
using Research.SharedKernel.Results;

namespace Research.Shared.Core.Mediation;

/// <summary>Represents the next step in the request pipeline (either the next behavior or the terminal handler).</summary>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken);

/// <summary>Chain-of-responsibility middleware wrapping every <see cref="ISender.SendAsync{TResponse}"/> execution.</summary>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}

/// <summary>Represents the next step in the domain event pipeline.</summary>
public delegate Task EventHandlerDelegate(CancellationToken cancellationToken);

/// <summary>Chain-of-responsibility middleware wrapping every <see cref="IPublisher.PublishAsync{TEvent}"/> execution.</summary>
public interface IEventPipelineBehavior<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(
        TEvent domainEvent,
        EventHandlerDelegate next,
        CancellationToken cancellationToken);
}

/// <summary>Strategy for validating a request before its handler runs; returns <c>null</c> when valid or an <see cref="Error"/> when invalid.</summary>
public interface IRequestValidator<in TRequest>
{
    ValueTask<Error?> ValidateAsync(TRequest request, CancellationToken cancellationToken);
}
