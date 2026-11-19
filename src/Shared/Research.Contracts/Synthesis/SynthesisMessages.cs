namespace Research.Contracts.Synthesis;

/// <summary>Jobs asks Synthesis to write the report from the job's indexed passages.</summary>
public sealed record SynthesisRequested(
    Guid JobId,
    string StepKey,
    string Question,
    IReadOnlyList<string> SubQuestions,
    int TopK) : IIntegrationMessage
{
    public static string MessageType => "synthesis.synthesis-requested";

    public static MessageKind Kind => MessageKind.Command;
}

/// <summary>The finished report in Markdown, with numbered citations matching the <c>[n]</c> markers in the text.</summary>
public sealed record ReportReady(
    Guid JobId,
    string StepKey,
    string Content,
    IReadOnlyList<ReportCitation> Citations,
    bool Truncated,
    long InputTokens,
    long OutputTokens,
    string Model) : IIntegrationMessage
{
    public static string MessageType => "synthesis.report-ready";

    public static MessageKind Kind => MessageKind.Event;
}

/// <summary>One numbered source of a report.</summary>
public sealed record ReportCitation(int Number, string Url, string Title);
