namespace Research.Contracts;

/// <summary>
/// How many services consume a contract (PLAN §11). It decides how the broker treats a message nobody can receive: a
/// command must reach its one consumer, so that is an error; an event may have no consumer at all.
/// </summary>
public enum MessageKind
{
    /// <summary>Asks one service to do something (<c>PlanRequested</c>); exactly one consuming service.</summary>
    Command,

    /// <summary>Announces something that happened (<c>JobCreated</c>, <c>PlanReady</c>); zero or more consumers.</summary>
    Event,
}
