using System.Collections.Frozen;
using System.Reflection;

namespace Research.Shared.Contracts;

/// <summary>
/// Every contract in this assembly by wire name, for code that sees only the name: the outbox publisher reads
/// <c>message_type</c> from a row, not a contract type.
/// </summary>
public static class MessageCatalog
{
    private static readonly FrozenDictionary<string, MessageKind> Kinds = typeof(IIntegrationMessage).Assembly
        .GetTypes()
        .Where(static type => type is { IsClass: true, IsAbstract: false } && type.IsAssignableTo(typeof(IIntegrationMessage)))
        .ToFrozenDictionary(
            static type => (string)ReadStatic(type, nameof(IIntegrationMessage.MessageType)),
            static type => (MessageKind)ReadStatic(type, nameof(IIntegrationMessage.Kind)),
            StringComparer.Ordinal);

    /// <summary>The wire names of all contracts.</summary>
    public static IEnumerable<string> MessageTypes => Kinds.Keys;

    /// <returns>The contract's kind. A name no contract has counts as a command, so it is never silently dropped.</returns>
    public static MessageKind KindOf(string messageType) =>
        Kinds.TryGetValue(messageType, out var kind) ? kind : MessageKind.Command;

    private static object ReadStatic(Type type, string property) =>
        type.GetProperty(property, BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
        ?? throw new InvalidOperationException($"{type.Name} does not implement {nameof(IIntegrationMessage)}.{property}.");
}
