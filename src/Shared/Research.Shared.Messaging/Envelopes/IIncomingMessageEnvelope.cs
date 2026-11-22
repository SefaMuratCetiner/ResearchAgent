namespace Research.Shared.Messaging;

/// <summary>Non-generic view of an incoming integration message request so pipeline behaviors (such as inbox deduplication) can read <see cref="Context"/> without reflection.</summary>
public interface IIncomingMessageEnvelope
{
    MessageContext Context { get; }
}
