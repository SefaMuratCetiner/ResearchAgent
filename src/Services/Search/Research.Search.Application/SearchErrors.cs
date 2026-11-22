using Research.Shared.Core.Errors;
using Research.Shared.Ports.Search;
using Research.SharedKernel.Results;

namespace Research.Search.Application;

/// <summary>Adds <see cref="SearchErrors"/> to the error catalog.</summary>
public sealed class SearchErrorCatalog : IErrorCatalogContributor
{
    public string Prefix => "search.";

    public IEnumerable<Error> GetErrors() => [SearchErrors.InvalidQuery, SearchErrors.QuotaExhausted, SearchErrors.ProviderRefused];
}
