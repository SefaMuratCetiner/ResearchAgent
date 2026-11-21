using Research.Shared.Core.Errors;
using Research.SharedKernel.Results;

namespace Research.Synthesis.Application;

/// <summary>Every error code the Synthesis service owns.</summary>
public static class SynthesisErrors
{
    public static readonly Error LlmRefused = new("synthesis.llm_refused", ErrorKind.Rule, "The model refused to write the report.");

    public static readonly Error NoPassages = new("synthesis.no_passages", ErrorKind.Rule, "No indexed passages were found for the job.");

    public static readonly Error EmptyReport = new("synthesis.empty_report", ErrorKind.Rule, "The model returned an empty report.");

    /// <summary>Not a failure: the report hit the output limit. Jobs records it as a warning.</summary>
    public static readonly Error OutputTruncated = new("synthesis.output_truncated", ErrorKind.Rule, "The report was cut at the output limit.");

    public static readonly Error UncitedReport = new("synthesis.uncited_report", ErrorKind.Rule, "The report cites none of the sources, so it is not kept.");
}

/// <summary>Adds <see cref="SynthesisErrors"/> to the error catalog.</summary>
public sealed class SynthesisErrorCatalog : IErrorCatalogContributor
{
    public string Prefix => "synthesis.";

    public IEnumerable<Error> GetErrors() =>
        [SynthesisErrors.LlmRefused, SynthesisErrors.NoPassages, SynthesisErrors.EmptyReport, SynthesisErrors.OutputTruncated, SynthesisErrors.UncitedReport];
}
