using Research.Shared.Kernel.Results;

namespace Research.Shared.Core.Search;

/// <summary>
/// Every error code the Search capability owns. An empty result is not an error: it is an empty <c>ResultsFound</c>.
/// A provider that is down is transient (retried) and ends as <c>research.step_exhausted</c>.
/// </summary>
public static class SearchErrors
{
    public static readonly Error InvalidQuery = new("search.invalid_query", ErrorKind.Invalid, "The search provider rejected the query.");

    public static readonly Error QuotaExhausted = new("search.quota_exhausted", ErrorKind.Rule, "The search provider's usage limit was reached.");

    public static readonly Error ProviderRefused = new("search.provider_refused", ErrorKind.Rule, "The search provider refused the query.");

    public static readonly ErrorSet All = new("search.", [InvalidQuery, QuotaExhausted, ProviderRefused]);
}
