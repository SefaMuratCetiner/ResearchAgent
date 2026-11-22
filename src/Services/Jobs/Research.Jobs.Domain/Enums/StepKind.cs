namespace Research.Jobs.Domain;

/// <summary>What a step asks of which service.</summary>
public enum StepKind
{
    Plan,
    Search,

    /// <summary>Fetch and index one page: Fetching downloads it, Knowledge indexes it and answers for the step.</summary>
    Fetch,
    Synthesize,
}
