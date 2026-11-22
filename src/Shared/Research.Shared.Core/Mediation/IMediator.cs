namespace Research.Shared.Core.Mediation;

/// <summary>Unified in-process mediator covering commands, queries, integration messages, and domain events.</summary>
public interface IMediator : ISender, IPublisher;
