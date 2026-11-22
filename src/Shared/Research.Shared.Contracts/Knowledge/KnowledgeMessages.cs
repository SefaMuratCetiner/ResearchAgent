namespace Research.Shared.Contracts.Knowledge;

/// <summary>
/// The document is chunked, embedded and linked to the job. <see cref="Reused"/> is true when the same content was
/// already indexed (by this or another job) and only the link was added.
/// </summary>
public sealed record DocumentIndexed(Guid JobId, string StepKey, Guid DocumentId, int PassageCount, bool Reused) : IIntegrationMessage
{
    public static string MessageType => "knowledge.document-indexed";

    public static MessageKind Kind => MessageKind.Event;
}
