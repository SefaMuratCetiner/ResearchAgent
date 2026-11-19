using Research.SharedKernel.Errors;
using Research.SharedKernel.Results;

namespace Research.Search;

/// <summary>
/// Every error code the Search service owns. An empty result is not an error: it is an empty <c>ResultsFound</c>.
/// A provider that is down is transient (retried) and ends as <c>research.step_exhausted</c>.
/// </summary>
public static class SearchErrors
{
    public static readonly Error InvalidQuery = new("search.invalid_query", ErrorKind.Invalid, "The search provider rejected the query.");

    public static readonly Error QuotaExhausted = new("search.quota_exhausted", ErrorKind.Rule, "The search provider's usage limit was reached.");

    public static readonly Error ProviderRefused = new("search.provider_refused", ErrorKind.Rule, "The search provider refused the query.");
}

/// <summary>Adds <see cref="SearchErrors"/> to the error catalog.</summary>
public sealed class SearchErrorCatalog : IErrorCatalogContributor
{
    public string Prefix => "search.";

    public IEnumerable<Error> GetErrors() => [SearchErrors.InvalidQuery, SearchErrors.QuotaExhausted, SearchErrors.ProviderRefused];
}
