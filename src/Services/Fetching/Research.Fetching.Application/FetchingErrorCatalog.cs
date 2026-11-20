using Research.Fetching.Domain;
using Research.Shared.Core.Errors;
using Research.SharedKernel.Results;

namespace Research.Fetching;

/// <summary>Adds <see cref="FetchingErrors"/> to the error catalog.</summary>
public sealed class FetchingErrorCatalog : IErrorCatalogContributor
{
    public string Prefix => "fetching.";

    public IEnumerable<Error> GetErrors() =>
    [
        FetchingErrors.FetchDisallowed,
        FetchingErrors.BlockedAddress,
        FetchingErrors.InvalidUrl,
        FetchingErrors.UnsupportedContent,
        FetchingErrors.TooLarge,
        FetchingErrors.ContentTooShort,
        FetchingErrors.RedirectLimit,
        FetchingErrors.HttpError,
        FetchingErrors.Timeout,
        FetchingErrors.BlockedDomain,
        FetchingErrors.SuspectedInjection,
    ];
}
