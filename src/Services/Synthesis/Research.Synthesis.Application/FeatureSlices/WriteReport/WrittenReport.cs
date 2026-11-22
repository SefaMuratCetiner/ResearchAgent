using Research.Synthesis.Application.Policies;

namespace Research.Synthesis.Application.WriteReport;

/// <summary>The report, the sources it cites, and what it cost.</summary>
public sealed record WrittenReport(
    string Content,
    IReadOnlyList<CitationBuilder.Source> Citations,
    bool Truncated,
    long InputTokens,
    long OutputTokens,
    string Model);
